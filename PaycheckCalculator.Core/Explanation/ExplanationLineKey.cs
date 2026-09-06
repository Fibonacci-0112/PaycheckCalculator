namespace PaycheckCalculator.Core.Explanation;

/// <summary>
/// Identifies which paycheck line an <see cref="LineExplanation"/> describes.
/// Used by the UI to look up the explanation behind a specific result row.
/// </summary>
public enum ExplanationLineKey
{
    GrossPay,
    FederalTaxableIncome,
    FicaTaxableWages,
    StateTaxableWages,
    PreTaxDeductions,
    FederalWithholding,
    SocialSecurity,
    Medicare,
    AdditionalMedicare,
    StateWithholding,
    StateCountyIncome,
    StateLocalIncome,
    StateDisability,
    NetPay
}
