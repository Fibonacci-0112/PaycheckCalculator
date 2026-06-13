using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Indiana;

/// <summary>
/// State module for Indiana.  Flat state income tax with WH-4 exemption-based
/// subtractions (personal/spouse/age/blind) and a separate additional
/// dependent exemption per WH-4 Line 4.
///
/// Sources:
///   • Indiana Form WH-4 "Employee's Withholding Exemption and County Status
///     Certificate" — annual exemption amounts of $1,000 per personal/age/blind
///     exemption, and $3,000 per additional dependent exemption
///     (Indiana Departmental Notice #1, effective for tax years beginning
///     after 2022; prior to 2023 this was $1,500).
///   • Indiana Departmental Notice #1 — Indiana's flat state adjusted gross
///     income tax rate.
///
/// Indiana also levies a county income tax that varies by county of residence
/// and principal work.  That component is out of scope here and is not
/// modeled by this calculator.
///
/// Calculation steps (Departmental Notice #1, "Income Tax Withholding" —
/// annualized method):
///   1. Annual exemption allowance =
///        (Exemptions × $1,000) + (DependentExemptions × $3,000).
///   2. Per-period exemption allowance = annual exemption ÷ pay periods per year.
///   3. State taxable wages per period = gross wages − pre-tax deductions
///      that reduce state wages (floored at $0).
///   4. Taxable amount = max(0, state taxable wages − per-period exemption).
///   5. State withholding = taxable amount × 3.05%, rounded to two decimal places.
///   6. Add any extra per-period withholding the employee requested on
///      WH-4 Line 6.
/// </summary>
public sealed class IndianaWithholdingCalculator : IStateWithholdingCalculator
{
    /// <summary>Indiana flat state adjusted gross income tax rate for 2026 (3.05%).</summary>
    private const decimal FlatRate = 0.0305m;

    /// <summary>Annual value of one personal/spouse/age/blind WH-4 exemption.</summary>
    private const decimal ExemptionAmount = 1_000m;

    /// <summary>Annual value of one WH-4 Line 4 additional dependent exemption (2023+).</summary>
    private const decimal DependentExemptionAmount = 3_000m;

    public UsState State => UsState.IN;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        var exemptions = values.GetValueOrDefault("Exemptions", 0);
        if (exemptions < 0)
            errors.Add("WH-4 Exemptions cannot be negative.");

        var dependentExemptions = values.GetValueOrDefault("DependentExemptions", 0);
        if (dependentExemptions < 0)
            errors.Add("Additional Dependent Exemptions cannot be negative.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var exemptions = values.GetValueOrDefault("Exemptions", 0);
        var dependentExemptions = values.GetValueOrDefault("DependentExemptions", 0);
        var extraWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        // Step 3: State taxable wages (pre-tax deductions reduce state wages).
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = context.PayPeriodsPerYear;

        // Step 1: Annual exemption from WH-4 personal/age/blind and dependent exemptions.
        decimal annualExemption = (exemptions * ExemptionAmount)
                                + (dependentExemptions * DependentExemptionAmount);

        // Step 2: Per-period exemption allowance.
        decimal exemptionPerPeriod = annualExemption / periods;

        // Step 4: Taxable amount after exemptions (floored at zero).
        decimal taxableAmount = Math.Max(0m, taxableWages - exemptionPerPeriod);

        // Step 5: Flat 3.05% state withholding, rounded to cents.
        decimal withholding = Math.Round(taxableAmount * FlatRate, 2, MidpointRounding.AwayFromZero);

        // Step 6: Add extra per-pay withholding from WH-4 Line 6.
        withholding += extraWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }
}
