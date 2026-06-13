using PaycheckCalc.Core.Models;

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
);
