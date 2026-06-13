using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;

namespace PaycheckCalc.Core.Tax.State;

/// <summary>
/// Universal payroll context shared by every state calculator.
/// Contains the fields that virtually all states need for withholding.
/// <para>
/// State-specific values (filing status choices, allowances, dependents, etc.)
/// live in <see cref="StateInputValues"/> instead.
/// </para>
/// </summary>
public sealed record CommonWithholdingContext
(
    /// <summary>Two-letter USPS state code (e.g., "OK", "AL").</summary>
    UsState State,

    /// <summary>Gross wages for the current pay period before any deductions.</summary>
    decimal GrossWages,

    /// <summary>How often the employee is paid.</summary>
    PayFrequency PayPeriod,

    /// <summary>Tax year (e.g., 2026).</summary>
    int Year,

    /// <summary>
    /// Sum of pre-tax deductions that reduce state taxable wages
    /// (e.g., 401k, health insurance, HSA).
    /// </summary>
    decimal PreTaxDeductionsReducingStateWages = 0m,

    /// <summary>
    /// Federal income tax withholding for the current pay period,
    /// computed before state tax. States like Alabama deduct this
    /// from gross income when calculating state taxable wages.
    /// </summary>
    decimal FederalWithholdingPerPeriod = 0m
)
{
    private readonly int? _payPeriodsPerYear;

    /// <summary>
    /// Number of pay periods in the year — the annualization factor every state calculator
    /// must use. When set (by the pay pipeline) it is resolved against the anchor pay date
    /// via <see cref="PayPeriods.PerYear(PayFrequency, System.DateOnly?)"/>, so it can be 53
    /// (weekly) or 27 (biweekly) in years that land an extra paycheck. When not set, it falls
    /// back to the fixed per-frequency count for <see cref="PayPeriod"/>.
    /// </summary>
    public int PayPeriodsPerYear
    {
        get => _payPeriodsPerYear ?? PayPeriods.PerYear(PayPeriod);
        init => _payPeriodsPerYear = value;
    }
}
