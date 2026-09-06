namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Turns whatever a state calculator returned into the ordered list of tax lines
/// the paycheck displays.
/// <para>
/// Most jurisdictions levy one income tax and at most one payroll assessment, and
/// express that through the scalar members of <see cref="StateWithholdingResult"/>.
/// Those are synthesized into lines here, so only the states that genuinely levy
/// several — New Jersey's SDI and FLI, Maryland's state and county income tax —
/// need to populate <see cref="StateWithholdingResult.TaxLines"/>.
/// </para>
/// </summary>
public static class StateTaxLineResolver
{
    /// <summary>Default label for a jurisdiction's personal income tax line.</summary>
    public const string StateIncomeLabel = "State Income Tax";

    /// <summary>
    /// Resolves and orders the display lines, with every amount rounded to the cent
    /// away from zero — these are the figures the user sees, and net pay is derived
    /// from them so the paycheck balances exactly.
    /// </summary>
    public static IReadOnlyList<StateTaxLine> Resolve(StateWithholdingResult stateResult)
    {
        ArgumentNullException.ThrowIfNull(stateResult);

        var lines = stateResult.TaxLines is { Count: > 0 }
            ? stateResult.TaxLines
            : Synthesize(stateResult);

        return StateTaxLineOrdering.Order(lines.Select(Round));
    }

    /// <summary>
    /// Builds display lines from the scalar amounts alone, for callers that hold a
    /// result without itemized lines — a hand-constructed <c>PaycheckResult</c>, or
    /// one restored from a snapshot saved before the itemized model existed.
    /// </summary>
    public static IReadOnlyList<StateTaxLine> FromTotals(
        decimal stateWithholding,
        decimal assessment,
        string assessmentLabel)
        => StateTaxLineOrdering.Order(new[]
        {
            new StateTaxLine
            {
                Kind = StateTaxLineKind.StateIncome,
                Label = StateIncomeLabel,
                Amount = stateWithholding
            },
            new StateTaxLine
            {
                Kind = StateTaxLineKind.PayrollAssessment,
                Label = assessmentLabel,
                Amount = assessment
            }
        });

    private static IEnumerable<StateTaxLine> Synthesize(StateWithholdingResult stateResult)
    {
        yield return new StateTaxLine
        {
            Kind = StateTaxLineKind.StateIncome,
            Label = StateIncomeLabel,
            Amount = stateResult.Withholding,
            Steps = stateResult.WithholdingSteps,
            Reference = stateResult.WithholdingReference
        };

        yield return new StateTaxLine
        {
            Kind = StateTaxLineKind.PayrollAssessment,
            Label = stateResult.DisabilityInsuranceLabel,
            Amount = stateResult.DisabilityInsurance,
            Steps = stateResult.DisabilityInsuranceSteps,
            Reference = stateResult.DisabilityInsuranceReference
        };
    }

    private static StateTaxLine Round(StateTaxLine line) => new()
    {
        Kind = line.Kind,
        Label = line.Label,
        Amount = Math.Round(line.Amount, 2, MidpointRounding.AwayFromZero),
        ShortCode = line.ShortCode,
        Steps = line.Steps,
        Reference = line.Reference
    };
}
