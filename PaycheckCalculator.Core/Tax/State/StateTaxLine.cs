using PaycheckCalculator.Core.Explanation;

namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Which kind of state-level tax a <see cref="StateTaxLine"/> represents.
/// The order of the members is the display order used by
/// <see cref="StateTaxLineOrdering"/>.
/// </summary>
public enum StateTaxLineKind
{
    /// <summary>State personal income tax withholding.</summary>
    StateIncome,

    /// <summary>County income tax (e.g. Maryland's county rate, Indiana's county tax).</summary>
    CountyIncome,

    /// <summary>Municipal / school-district income tax (e.g. NYC, Yonkers, PA EIT).</summary>
    LocalIncome,

    /// <summary>
    /// Employee-paid payroll assessment funding a state program — disability
    /// insurance, paid family/medical leave, long-term care, or employee SUI.
    /// </summary>
    PayrollAssessment
}

/// <summary>
/// One state-level line on the paycheck. A state calculator returns as many of
/// these as the jurisdiction actually levies: New Jersey emits SDI and FLI
/// separately, Maryland emits state and county income tax, and most states emit
/// a single <see cref="StateTaxLineKind.StateIncome"/> line.
/// </summary>
public sealed class StateTaxLine
{
    /// <summary>What kind of tax this line is. Drives display order and grouping.</summary>
    public required StateTaxLineKind Kind { get; init; }

    /// <summary>Display label, e.g. "State Income Tax" or "Disability Insurance (SDI)".</summary>
    public required string Label { get; init; }

    /// <summary>The amount withheld this pay period, rounded to the cent by the caller.</summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Short program code ("SDI", "FLI", "PFML") used by compact surfaces such as
    /// CSV columns and A/B comparison rows. Null for ordinary income-tax lines.
    /// </summary>
    public string? ShortCode { get; init; }

    /// <summary>
    /// Optional "Show Your Work" steps. When null, <c>PayCalculator</c> synthesizes
    /// a generic one-step breakdown so every visible line stays explainable.
    /// </summary>
    public IReadOnlyList<ExplanationStep>? Steps { get; init; }

    /// <summary>Optional human-readable citation shown beneath the steps.</summary>
    public string? Reference { get; init; }

    /// <summary>
    /// Stable identifier distinguishing this line from its siblings of the same
    /// <see cref="Kind"/>, used to look up its explanation. Defaults to
    /// <see cref="ShortCode"/> and falls back to <see cref="Label"/>.
    /// </summary>
    public string ExplanationSubKey => ShortCode ?? Label;
}
