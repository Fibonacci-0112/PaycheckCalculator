using PaycheckCalculator.Core.Explanation;

namespace PaycheckCalculator.Core.Models;

/// <summary>
/// Result of a self-employment / 1099 calculation: the federal self-employment tax
/// (Social Security + Medicare, both halves) on 92.35% of net earnings, the estimated
/// state income tax on those earnings, the resulting take-home, and a quarterly
/// estimated-payment schedule (Form 1040-ES) for paying it across the year.
/// </summary>
public sealed class SelfEmploymentResult
{
    /// <summary>Annual net self-employment earnings the result was computed for (Schedule C net profit).</summary>
    public decimal AnnualNetEarnings { get; init; }

    /// <summary>Net earnings subject to self-employment tax — <see cref="AnnualNetEarnings"/> × 92.35%.</summary>
    public decimal NetEarningsSubjectToSeTax { get; init; }

    /// <summary>The tax year this result was calculated under (from the originating input's <c>TaxYear</c>).</summary>
    public int TaxYear { get; init; } = TaxYearSupport.Default;

    public UsState State { get; init; }

    /// <summary>Social Security portion of SE tax (12.4%), capped at the annual wage base.</summary>
    public decimal SocialSecurityTax { get; init; }

    /// <summary>Medicare portion of SE tax (2.9%), no wage cap.</summary>
    public decimal MedicareTax { get; init; }

    /// <summary>Additional Medicare tax (0.9%) on net earnings above the $200,000 threshold.</summary>
    public decimal AdditionalMedicareTax { get; init; }

    /// <summary>Total federal self-employment tax (Social Security + Medicare + Additional Medicare).</summary>
    public decimal SelfEmploymentTax { get; init; }

    /// <summary>
    /// Estimated state income tax on the earnings. Zero in the nine states with no income
    /// tax. There is no separate <i>state</i> self-employment tax anywhere in the U.S. —
    /// states tax self-employment income as ordinary income.
    /// </summary>
    public decimal StateIncomeTax { get; init; }

    /// <summary>Human-readable summary of how the state amount was derived (or why it is zero).</summary>
    public string StateIncomeTaxDescription { get; init; } = "";

    /// <summary>Take-home before state income tax (<see cref="AnnualNetEarnings"/> − <see cref="SelfEmploymentTax"/>).</summary>
    public decimal TakeHomeBeforeStateTax { get; init; }

    /// <summary>Take-home after self-employment tax and estimated state income tax.</summary>
    public decimal TakeHome { get; init; }

    /// <summary>Total estimated tax (self-employment tax + state income tax).</summary>
    public decimal TotalTax => SelfEmploymentTax + StateIncomeTax;

    /// <summary>Federal portion of each equal quarterly estimated payment (self-employment tax ÷ 4).</summary>
    public decimal FederalQuarterlyPayment { get; init; }

    /// <summary>State portion of each equal quarterly estimated payment (state income tax ÷ 4).</summary>
    public decimal StateQuarterlyPayment { get; init; }

    /// <summary>The four quarterly estimated-payment installments and their due dates.</summary>
    public IReadOnlyList<QuarterlyEstimate> QuarterlyEstimates { get; init; } = Array.Empty<QuarterlyEstimate>();

    /// <summary>"Show Your Work" breakdown — one line per visible row, mirroring <see cref="PaycheckResult"/>.</summary>
    public PaycheckExplanation Explanation { get; init; } = PaycheckExplanation.Empty;
}

/// <summary>
/// One quarterly estimated-payment installment for a self-employment year (IRS Form
/// 1040-ES). The federal amount goes to the IRS; the state amount, when non-zero, goes
/// to the state revenue agency. Most states use the same due dates as the IRS.
/// </summary>
/// <param name="Label">Short quarter label, e.g. "Q1".</param>
/// <param name="Period">Income period the installment covers, e.g. "Jan 1 – Mar 31, 2026".</param>
/// <param name="DueDate">Date the installment is due.</param>
/// <param name="FederalAmount">Federal self-employment-tax portion of the installment.</param>
/// <param name="StateAmount">Estimated state income-tax portion of the installment.</param>
public sealed record QuarterlyEstimate(
    string Label,
    string Period,
    DateOnly DueDate,
    decimal FederalAmount,
    decimal StateAmount)
{
    /// <summary>Combined federal + state amount due for the quarter.</summary>
    public decimal TotalAmount => FederalAmount + StateAmount;
}
