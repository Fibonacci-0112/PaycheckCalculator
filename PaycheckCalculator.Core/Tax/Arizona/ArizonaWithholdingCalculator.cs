using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Core.Tax.Arizona;

/// <summary>
/// State module for Arizona.
/// <para>
/// Unlike most states, Arizona's employer withholding is a direct
/// percentage election on Form A-4 ("Employee's Arizona Withholding
/// Election").  The employee picks one of seven flat rates — 0.5%, 1.0%,
/// 1.5%, 2.0%, 2.5%, 3.0%, or 3.5% — which the employer then applies to
/// gross taxable wages each pay period.  Filing status, allowances, and
/// dependents do <b>not</b> factor in to the per-period calculation; the
/// reconciliation happens on the annual Form 140.
/// </para>
/// <para>
/// Source: Arizona Department of Revenue, 2026 Form A-4 "Employee's
/// Arizona Withholding Election" and Publication "Arizona Withholding
/// Percentage Election."  When an employee does not file a valid A-4,
/// the employer must default to 2.0%.
/// </para>
/// Calculation steps:
/// <list type="number">
///   <item>State taxable wages per period = gross wages − pre-tax
///         deductions that reduce state wages (floored at $0).</item>
///   <item>Withholding = taxable wages × elected rate, rounded to two
///         decimal places (away-from-zero, matching the other flat-rate
///         state calculators in this repo).</item>
///   <item>Add any extra per-period withholding the employee requested
///         on Form A-4, Line 2.</item>
/// </list>
/// </summary>
public sealed class ArizonaWithholdingCalculator : IStateWithholdingCalculator
{
    /// <summary>
    /// The seven Form A-4 withholding percentages, indexed by their
    /// display label.  Values are the decimal rates applied to gross
    /// taxable wages each pay period.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, decimal> A4Rates =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["0.5%"] = 0.005m,
            ["1.0%"] = 0.010m,
            ["1.5%"] = 0.015m,
            ["2.0%"] = 0.020m,
            ["2.5%"] = 0.025m,
            ["3.0%"] = 0.030m,
            ["3.5%"] = 0.035m
        };

    /// <summary>Default rate used when no valid A-4 is on file (2.0%).</summary>
    internal const string DefaultRateLabel = "2.0%";

    public UsState State => UsState.AZ;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        var rate = values.GetValueOrDefault<string>("WithholdingRate", DefaultRateLabel);
        if (!string.IsNullOrWhiteSpace(rate) && !A4Rates.ContainsKey(rate))
            errors.Add($"A-4 Withholding Rate '{rate}' is not a valid Arizona election.");

        var extra = values.GetValueOrDefault("AdditionalWithholding", 0m);
        if (extra < 0m)
            errors.Add("Extra Withholding cannot be negative.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        // Step 1: State taxable wages (pre-tax deductions reduce state wages).
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        // Resolve the employee's A-4 percentage election.  An unknown or
        // missing label falls back to the Arizona default of 2.0%, which
        // mirrors the employer's legal obligation when no valid A-4 is
        // on file.
        var rateLabel = values.GetValueOrDefault<string>("WithholdingRate", DefaultRateLabel);
        if (string.IsNullOrWhiteSpace(rateLabel) || !A4Rates.TryGetValue(rateLabel, out var rate))
            rate = A4Rates[DefaultRateLabel];

        // Step 2: Flat percentage of taxable wages, rounded to cents.
        var withholding = Math.Round(taxableWages * rate, 2, MidpointRounding.AwayFromZero);

        // Step 3: Add any employee-requested extra withholding (A-4 Line 2).
        withholding += values.GetValueOrDefault("AdditionalWithholding", 0m);

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }
}
