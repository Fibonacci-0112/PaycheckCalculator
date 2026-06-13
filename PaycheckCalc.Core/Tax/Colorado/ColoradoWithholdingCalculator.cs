using System.Text.Json;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Colorado;

/// <summary>
/// State module for Colorado.  Flat 4.4% income tax with a filing-status-based
/// annual withholding allowance (DR 0004 Table 1) that reduces taxable income,
/// plus Family and Medical Leave Insurance (FMLI) at 0.044% of gross wages.
///
/// Calculation steps (per DR 0004):
///   1. Annualize taxable wages (wages × pay periods).
///   2. Subtract the Table 1 allowance for the employee's filing status and
///      number of jobs.
///   3. Multiply the result by 4.4%.
///   4. Divide by the number of pay periods and round.
/// </summary>
public sealed class ColoradoWithholdingCalculator : IStateWithholdingCalculator
{
    /// <summary>Colorado flat income tax rate (4.4%).</summary>
    private const decimal FlatRate = 0.044m;

    /// <summary>Colorado FMLI premium rate (0.044%).</summary>
    private const decimal FmliRate = 0.00044m;

    private readonly IReadOnlyList<CoDr0004Allowance> _allowances;
    private readonly IReadOnlyList<string> _filingStatusOptions;
    private readonly IReadOnlyList<string> _numberOfJobsOptions;

    public ColoradoWithholdingCalculator(string json, IStateSchemaProvider schemaProvider)
    {
        var root = JsonSerializer.Deserialize<CoDr0004Root>(json,
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? throw new InvalidOperationException("Failed to deserialize Colorado DR 0004 JSON data.");

        _allowances = root.Allowances;
        _filingStatusOptions = schemaProvider.GetOptions(UsState.CO, "FilingStatus");
        _numberOfJobsOptions = schemaProvider.GetOptions(UsState.CO, "NumberOfJobs");
    }

    public UsState State => UsState.CO;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");

        var jobs = values.GetValueOrDefault<string>("NumberOfJobs", "");
        if (!_numberOfJobsOptions.Contains(jobs))
            errors.Add($"Number of Jobs must be one of: {string.Join(", ", _numberOfJobsOptions)}.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 1: Annualize taxable wages
        var annualWages = taxableWages * periods;

        // Step 2: Subtract Table 1 allowance based on filing status and number of jobs
        var filingStatus = values.GetValueOrDefault("FilingStatus", "Single or Married Filing Separately");
        var numberOfJobs = values.GetValueOrDefault("NumberOfJobs", "1");
        var allowance = LookupAllowance(filingStatus, numberOfJobs);
        var adjustedWages = Math.Max(0m, annualWages - allowance);

        // Step 3: Multiply by 4.4%
        var annualTax = adjustedWages * FlatRate;

        // Step 4: Divide by number of pay periods and round
        var periodTax = annualTax / periods;

        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero)
                        + values.GetValueOrDefault("AdditionalWithholding", 0m);

        // FMLI: 0.044% of ALL gross wages (no wage cap)
        var fmli = Math.Round(Math.Max(0m, context.GrossWages) * FmliRate, 2, MidpointRounding.AwayFromZero);

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding,
            DisabilityInsurance = fmli
        };
    }

    /// <summary>
    /// Look up the annual withholding allowance from DR 0004 Table 1
    /// based on filing status and number of jobs.
    /// </summary>
    private decimal LookupAllowance(string filingStatus, string numberOfJobs)
    {
        var entry = _allowances.FirstOrDefault(a =>
            string.Equals(a.FilingStatus, filingStatus, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return 0m;

        return numberOfJobs switch
        {
            "1" => entry.Jobs1,
            "2" => entry.Jobs2,
            "3" => entry.Jobs3,
            _ => entry.Jobs4OrMore  // "4" or any higher value
        };
    }

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
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };

    // ── JSON deserialization models ──────────────────────────────────

    private sealed class CoDr0004Root
    {
        public int SchemaVersion { get; set; }
        public List<CoDr0004Allowance> Allowances { get; set; } = [];
    }

    internal sealed class CoDr0004Allowance
    {
        public string FilingStatus { get; set; } = "";
        public decimal Jobs1 { get; set; }
        public decimal Jobs2 { get; set; }
        public decimal Jobs3 { get; set; }
        public decimal Jobs4OrMore { get; set; }
    }
}
