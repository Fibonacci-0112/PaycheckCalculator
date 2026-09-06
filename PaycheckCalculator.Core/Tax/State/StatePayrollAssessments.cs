using System.Text.Json;
using System.Text.Json.Serialization;
using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;

namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// The employee-paid disability / paid-leave / long-term-care premiums a state
/// withholds, loaded from <c>state_payroll_assessments_2026.json</c>.
/// <para>
/// These programs cap contributions three different ways and the table models
/// each one explicitly rather than approximating: an annual taxable wage base
/// (New Jersey, Massachusetts, Rhode Island, Oregon), a statutory per-week
/// dollar ceiling (New York's DBL, Hawaii's TDI), or a maximum contribution for
/// the whole year (New York's PFL).
/// </para>
/// </summary>
public sealed class StatePayrollAssessments
{
    private readonly Dictionary<UsState, IReadOnlyList<StatePayrollAssessment>> _byState;

    private StatePayrollAssessments(Dictionary<UsState, IReadOnlyList<StatePayrollAssessment>> byState) =>
        _byState = byState;

    /// <summary>The tax year the loaded table applies to.</summary>
    public int TaxYear { get; private init; }

    /// <summary>Parses the assessment table. Throws when the JSON is malformed or names an unknown state.</summary>
    public static StatePayrollAssessments Load(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var root = JsonSerializer.Deserialize<AssessmentRoot>(json, options)
            ?? throw new InvalidOperationException("State payroll assessment data could not be parsed.");

        var byState = new Dictionary<UsState, IReadOnlyList<StatePayrollAssessment>>();
        foreach (var (code, entry) in root.States)
        {
            if (!Enum.TryParse<UsState>(code, ignoreCase: true, out var state))
                throw new InvalidOperationException($"State payroll assessment data names unknown jurisdiction '{code}'.");

            byState[state] = entry.Programs.Select(p => p.ToAssessment(code)).ToList();
        }

        return new StatePayrollAssessments(byState) { TaxYear = root.TaxYear };
    }

    /// <summary>The programs <paramref name="state"/> withholds, or an empty list when it withholds none.</summary>
    public IReadOnlyList<StatePayrollAssessment> For(UsState state) =>
        _byState.TryGetValue(state, out var programs) ? programs : Array.Empty<StatePayrollAssessment>();

    /// <summary>
    /// Computes every assessment line <paramref name="state"/> levies for this pay
    /// period, skipping programs the employee is exempt from and any that compute
    /// to zero. Returns an empty list for the 45 states that levy none.
    /// </summary>
    public IReadOnlyList<StateTaxLine> BuildLines(
        UsState state,
        CommonWithholdingContext context,
        StateInputValues values)
    {
        var lines = new List<StateTaxLine>();
        foreach (var program in For(state))
        {
            var exempt = program.ExemptFieldKey is not null
                && values.GetValueOrDefault(program.ExemptFieldKey, false);
            if (exempt)
                continue;

            var line = program.Compute(context);
            if (line.Amount > 0m)
                lines.Add(line);
        }
        return lines;
    }

    private sealed class AssessmentRoot
    {
        [JsonPropertyName("taxYear")] public int TaxYear { get; set; }
        [JsonPropertyName("states")] public Dictionary<string, AssessmentStateDto> States { get; set; } = new();
    }

    private sealed class AssessmentStateDto
    {
        [JsonPropertyName("programs")] public List<AssessmentProgramDto> Programs { get; set; } = new();
    }

    private sealed class AssessmentProgramDto
    {
        [JsonPropertyName("label")] public string Label { get; set; } = "";
        [JsonPropertyName("shortCode")] public string ShortCode { get; set; } = "";
        [JsonPropertyName("rate")] public decimal Rate { get; set; }
        [JsonPropertyName("annualWageBase")] public decimal? AnnualWageBase { get; set; }
        [JsonPropertyName("maxWeeklyDeduction")] public decimal? MaxWeeklyDeduction { get; set; }
        [JsonPropertyName("maxAnnualContribution")] public decimal? MaxAnnualContribution { get; set; }
        [JsonPropertyName("exemptFieldKey")] public string? ExemptFieldKey { get; set; }
        [JsonPropertyName("reference")] public string Reference { get; set; } = "";

        public StatePayrollAssessment ToAssessment(string code)
        {
            if (string.IsNullOrWhiteSpace(Label) || string.IsNullOrWhiteSpace(ShortCode))
                throw new InvalidOperationException($"State payroll assessment for {code} requires a label and short code.");
            if (Rate <= 0m)
                throw new InvalidOperationException($"State payroll assessment '{Label}' ({code}) requires a positive rate.");
            if (string.IsNullOrWhiteSpace(Reference))
                throw new InvalidOperationException($"State payroll assessment '{Label}' ({code}) requires a source reference.");

            return new StatePayrollAssessment
            {
                Label = Label,
                ShortCode = ShortCode,
                Rate = Rate,
                AnnualWageBase = AnnualWageBase,
                MaxWeeklyDeduction = MaxWeeklyDeduction,
                MaxAnnualContribution = MaxAnnualContribution,
                ExemptFieldKey = ExemptFieldKey,
                Reference = Reference
            };
        }
    }
}
