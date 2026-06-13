using System.Text.Json;
using System.Text.Json.Serialization;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Connecticut;

/// <summary>
/// Connecticut state income tax withholding calculator implementing the
/// TPG-211, 2026 Withholding Calculation Rules.
///
/// Connecticut does not provide a separate percentage method.  Withholding
/// is determined by the table-driven algorithm using five lookup tables
/// keyed by the employee's CT-W4 withholding code (A–F):
///
///   1. Annualize wages:  S = wagesPerPeriod × payPeriodsPerYear
///   2. Table A → personal exemption (keyed by code + S)
///   3. Taxable income:   TI = max(S − exemption, 0)
///   4. Table B → initial tax (keyed by code + TI)
///   5. Table C → phase-out add-back (keyed by code + S)
///   6. Table D → tax recapture (keyed by code + S)
///   7. Table E → personal tax credit decimal (keyed by code + S)
///   8. Annual = (initialTax + addBack + recapture) × (1 − credit)
///   9. PerPeriod = max(Annual / periods + additional − reduced, 0)
///
/// Special rules:
///   • Code D: exemption = 0, credit = 0.00, shares Table B/C/D with A.
///   • Code E: base withholding is always 0 (additional withholding still applies).
///   • No Form CT-W4: flat 6.99% of taxable wages per period (no tables used).
///   • Table B uses taxable income; Tables C/D/E use annualized salary.
/// </summary>
public sealed class ConnecticutWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Lookup tables loaded from JSON ───────────────────────────────

    private readonly Dictionary<string, List<ExemptionEntry>> _personalExemptions;
    private readonly Dictionary<string, List<BracketEntry>> _initialTax;
    private readonly Dictionary<string, List<AddBackEntry>> _phaseOutAddBack;
    private readonly Dictionary<string, List<RecaptureEntry>> _taxRecapture;
    private readonly Dictionary<string, List<CreditEntry>> _personalTaxCredits;

    /// <summary>
    /// Flat rate applied to taxable wages when no CT-W4 form is on file (6.99%).
    /// </summary>
    private const decimal NoFormFlatRate = 0.0699m;

    /// <summary>
    /// Connecticut Paid Family and Medical Leave Insurance (PFMLI) employee
    /// contribution rate (0.5%) applied to all gross wages per period.
    /// </summary>
    private const decimal PfmliRate = 0.005m;

    /// <summary>Display label used for PFMLI on the results screen and exports.</summary>
    private const string PfmliLabel = "Family Leave Insurance (FLI)";

    private readonly IReadOnlyList<string> _withholdingCodeOptions;

    // ── Construction ────────────────────────────────────────────────

    public ConnecticutWithholdingCalculator(string json, IStateSchemaProvider schemaProvider)
    {
        var root = JsonSerializer.Deserialize<CtWithholdingRoot>(json,
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? throw new InvalidOperationException(
                       "Failed to deserialize Connecticut withholding JSON data.");

        var tables = root.Tables;

        _personalExemptions = DeserializeTable<ExemptionEntry>(tables.PersonalExemptions, out _);
        _initialTax = DeserializeTable<BracketEntry>(tables.InitialTaxCalculation, out var itRefs);
        _phaseOutAddBack = DeserializeTable<AddBackEntry>(tables.PhaseOutAddBack, out var abRefs);
        _taxRecapture = DeserializeTable<RecaptureEntry>(tables.TaxRecapture, out var rcRefs);
        _personalTaxCredits = DeserializeTable<CreditEntry>(tables.PersonalTaxCredits, out _);

        // Resolve "same_as_X" string references after initial deserialization
        ResolveReferences(_initialTax, itRefs);
        ResolveReferences(_phaseOutAddBack, abRefs);
        ResolveReferences(_taxRecapture, rcRefs);

        _withholdingCodeOptions = schemaProvider.GetOptions(UsState.CT, "WithholdingCode");
    }

    // ── IStateWithholdingCalculator ─────────────────────────────────

    public UsState State => UsState.CT;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        // Use the schema default ("Code A") as fallback so that a null/missing
        // value (e.g., from MAUI Picker binding initialization) doesn't produce
        // a spurious validation error that blocks the calculation.
        var code = values.GetValueOrDefault<string>("WithholdingCode", "Code A");
        if (!_withholdingCodeOptions.Contains(code))
            errors.Add($"Invalid Withholding Code. Valid options are: {string.Join(", ", _withholdingCodeOptions)}.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        var codeDisplay = values.GetValueOrDefault("WithholdingCode", "Code A");

        // Defensive: the dictionary value itself may be an empty or whitespace string
        // (as opposed to null/missing, which GetValueOrDefault already handles).
        if (string.IsNullOrWhiteSpace(codeDisplay))
            codeDisplay = "Code A";

        var code = codeDisplay.Replace("Code ", "");

        var additionalWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);
        var reducedWithholding = values.GetValueOrDefault("ReducedWithholding", 0m);

        // CT PFMLI: 0.5% of ALL gross wages (not reduced by pre-tax deductions)
        var pfmli = Math.Round(Math.Max(0m, context.GrossWages) * PfmliRate, 2, MidpointRounding.AwayFromZero);

        // No Form CT-W4: flat 6.99% of taxable wages per period
        if (codeDisplay == "No Form CT-W4")
        {
            var flatTax = taxableWages * NoFormFlatRate;
            var perPeriod = Math.Max(flatTax + additionalWithholding - reducedWithholding, 0m);
            return new StateWithholdingResult
            {
                TaxableWages = taxableWages,
                Withholding = Math.Round(perPeriod, 2, MidpointRounding.AwayFromZero),
                DisabilityInsurance = pfmli,
                DisabilityInsuranceLabel = PfmliLabel,
                Description = "No Form CT-W4 — taxable wages taxed at 6.99%"
            };
        }

        // Code E: no withholding unless additional withholding is specified
        if (code == "E")
        {
            var perPeriod = Math.Max(additionalWithholding - reducedWithholding, 0m);
            return new StateWithholdingResult
            {
                TaxableWages = taxableWages,
                Withholding = Math.Round(perPeriod, 2, MidpointRounding.AwayFromZero),
                DisabilityInsurance = pfmli,
                DisabilityInsuranceLabel = PfmliLabel,
                Description = perPeriod == 0m
                    ? "Code E — no Connecticut withholding required"
                    : null
            };
        }

        // Step 3: Annualize wages
        var annualizedSalary = taxableWages * periods;

        // Step 5: Table A — personal exemption (keyed by annualizedSalary)
        var personalExemption = LookupExemption(code, annualizedSalary);

        // Step 6: Taxable income
        var annualizedTaxableIncome = Math.Max(annualizedSalary - personalExemption, 0m);

        decimal basePerPeriodWithholding;

        if (annualizedTaxableIncome <= 0m)
        {
            // No tax due — skip to final adjustment
            basePerPeriodWithholding = 0m;
        }
        else
        {
            // Step 7: Table B — initial tax (keyed by annualizedTaxableIncome)
            var initialTax = LookupInitialTax(code, annualizedTaxableIncome);

            // Step 8: Table C — phase-out add-back (keyed by annualizedSalary)
            var phaseOutAddBack = LookupAddBack(code, annualizedSalary);

            // Step 9: Table D — tax recapture (keyed by annualizedSalary)
            var recapture = LookupRecapture(code, annualizedSalary);

            // Step 10: pre-credit annual tax
            var preCreditAnnualTax = initialTax + phaseOutAddBack + recapture;

            // Step 11: Table E — personal tax credit decimal (keyed by annualizedSalary)
            var personalTaxCredit = LookupCredit(code, annualizedSalary);

            // Step 12: annualized withholding
            var annualizedWithholding = preCreditAnnualTax * (1m - personalTaxCredit);

            // Step 13: per-period base withholding
            basePerPeriodWithholding = annualizedWithholding / periods;
        }

        // Step 14: final adjustment
        var finalWithholding = Math.Max(
            basePerPeriodWithholding + additionalWithholding - reducedWithholding, 0m);

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = Math.Round(finalWithholding, 2, MidpointRounding.AwayFromZero),
            DisabilityInsurance = pfmli,
            DisabilityInsuranceLabel = PfmliLabel
        };
    }

    // ── Table lookups ───────────────────────────────────────────────

    private decimal LookupExemption(string code, decimal annualizedSalary)
    {
        if (!_personalExemptions.TryGetValue(code, out var entries))
            return 0m;

        foreach (var e in entries)
        {
            if (annualizedSalary > e.Gt && (e.Lte is null || annualizedSalary <= e.Lte))
                return e.Exemption;
        }

        return 0m;
    }

    private decimal LookupInitialTax(string code, decimal taxableIncome)
    {
        if (!_initialTax.TryGetValue(code, out var entries))
            return 0m;

        foreach (var b in entries)
        {
            if (taxableIncome > b.Gt && (b.Lte is null || taxableIncome <= b.Lte))
                return b.BaseTax + b.Rate * (taxableIncome - b.ExcessOver);
        }

        return 0m;
    }

    private decimal LookupAddBack(string code, decimal annualizedSalary)
    {
        if (!_phaseOutAddBack.TryGetValue(code, out var entries))
            return 0m;

        foreach (var e in entries)
        {
            if (annualizedSalary > e.Gt && (e.Lte is null || annualizedSalary <= e.Lte))
                return e.AddBack;
        }

        return 0m;
    }

    private decimal LookupRecapture(string code, decimal annualizedSalary)
    {
        if (!_taxRecapture.TryGetValue(code, out var entries))
            return 0m;

        foreach (var e in entries)
        {
            if (annualizedSalary > e.Gt && (e.Lte is null || annualizedSalary <= e.Lte))
                return e.Recapture;
        }

        return 0m;
    }

    private decimal LookupCredit(string code, decimal annualizedSalary)
    {
        if (!_personalTaxCredits.TryGetValue(code, out var entries))
            return 0m;

        foreach (var e in entries)
        {
            if (annualizedSalary > e.Gt && (e.Lte is null || annualizedSalary <= e.Lte))
                return e.Credit;
        }

        return 0m;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static int GetPayPeriods(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily => 260,
        PayFrequency.Weekly => 52,
        PayFrequency.Biweekly => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly => 12,
        PayFrequency.Quarterly => 4,
        PayFrequency.Semiannual => 2,
        PayFrequency.Annual => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency,
            "Unsupported pay frequency")
    };

    /// <summary>
    /// Deserializes a table section from the JSON where each code key is
    /// either a JSON array of entry objects or a "same_as_X" string reference.
    /// Array entries are deserialized directly; string references are collected
    /// into <paramref name="references"/> for later resolution by
    /// <see cref="ResolveReferences{T}"/>.
    /// </summary>
    private static Dictionary<string, List<T>> DeserializeTable<T>(
        Dictionary<string, JsonElement> rawTable,
        out Dictionary<string, string> references)
    {
        var result = new Dictionary<string, List<T>>(StringComparer.OrdinalIgnoreCase);
        references = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        foreach (var (code, element) in rawTable)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                result[code] = element.Deserialize<List<T>>(opts) ?? [];
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                // e.g. "same_as_A" → target code "A"
                var raw = element.GetString() ?? "";
                var target = raw.StartsWith("same_as_", StringComparison.OrdinalIgnoreCase)
                    ? raw["same_as_".Length..]
                    : "A";
                references[code] = target;
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves string references (e.g. "same_as_A") collected during
    /// deserialization by pointing each referencing code at the target code's list.
    /// </summary>
    private static void ResolveReferences<T>(
        Dictionary<string, List<T>> table,
        Dictionary<string, string> references)
    {
        foreach (var (code, target) in references)
        {
            if (table.TryGetValue(target, out var targetList))
                table[code] = targetList;
        }
    }

    // ── JSON deserialization models ─────────────────────────────────

    private sealed class CtWithholdingRoot
    {
        [JsonPropertyName("tables")]
        public CtTables Tables { get; set; } = new();
    }

    private sealed class CtTables
    {
        [JsonPropertyName("personal_exemptions")]
        public Dictionary<string, JsonElement> PersonalExemptions { get; set; } = new();

        [JsonPropertyName("initial_tax_calculation")]
        public Dictionary<string, JsonElement> InitialTaxCalculation { get; set; } = new();

        [JsonPropertyName("phase_out_add_back")]
        public Dictionary<string, JsonElement> PhaseOutAddBack { get; set; } = new();

        [JsonPropertyName("tax_recapture")]
        public Dictionary<string, JsonElement> TaxRecapture { get; set; } = new();

        [JsonPropertyName("personal_tax_credits")]
        public Dictionary<string, JsonElement> PersonalTaxCredits { get; set; } = new();
    }

    private sealed class ExemptionEntry
    {
        [JsonPropertyName("gt")]
        public decimal Gt { get; set; }

        [JsonPropertyName("lte")]
        public decimal? Lte { get; set; }

        [JsonPropertyName("exemption")]
        public decimal Exemption { get; set; }
    }

    private sealed class BracketEntry
    {
        [JsonPropertyName("gt")]
        public decimal Gt { get; set; }

        [JsonPropertyName("lte")]
        public decimal? Lte { get; set; }

        [JsonPropertyName("base_tax")]
        public decimal BaseTax { get; set; }

        [JsonPropertyName("rate")]
        public decimal Rate { get; set; }

        [JsonPropertyName("excess_over")]
        public decimal ExcessOver { get; set; }
    }

    private sealed class AddBackEntry
    {
        [JsonPropertyName("gt")]
        public decimal Gt { get; set; }

        [JsonPropertyName("lte")]
        public decimal? Lte { get; set; }

        [JsonPropertyName("add_back")]
        public decimal AddBack { get; set; }
    }

    private sealed class RecaptureEntry
    {
        [JsonPropertyName("gt")]
        public decimal Gt { get; set; }

        [JsonPropertyName("lte")]
        public decimal? Lte { get; set; }

        [JsonPropertyName("recapture")]
        public decimal Recapture { get; set; }
    }

    private sealed class CreditEntry
    {
        [JsonPropertyName("gt")]
        public decimal Gt { get; set; }

        [JsonPropertyName("lte")]
        public decimal? Lte { get; set; }

        [JsonPropertyName("credit")]
        public decimal Credit { get; set; }
    }
}
