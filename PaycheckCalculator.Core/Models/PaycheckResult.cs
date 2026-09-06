using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Core.Models;

public sealed class PaycheckResult
{
    private readonly IReadOnlyList<StateTaxLine> _stateTaxLines = Array.Empty<StateTaxLine>();

    public decimal GrossPay { get; init; }
    public decimal PreTaxDeductions { get; init; }
    public decimal PostTaxDeductions { get; init; }

    /// <summary>The tax year this result was calculated under (from the originating input's <c>TaxYear</c>).</summary>
    public int TaxYear { get; init; } = TaxYearSupport.Default;

    public UsState State { get; init; }
    public decimal StateTaxableWages { get; init; }
    public decimal StateWithholding { get; init; }
    public decimal StateDisabilityInsurance { get; init; }
    public string StateDisabilityInsuranceLabel { get; init; } = "State Disability Insurance";

    /// <summary>
    /// Every state-level tax line this paycheck carries — state, county and local
    /// income tax plus each payroll assessment — already ordered for display by
    /// <see cref="StateTaxLineOrdering"/>. The scalar members above remain
    /// populated as the aggregate of these lines.
    /// <para>
    /// Falls back to synthesizing lines from those scalars when none were supplied,
    /// so a hand-constructed result or one restored from an older snapshot still
    /// renders and exports correctly.
    /// </para>
    /// </summary>
    public IReadOnlyList<StateTaxLine> StateTaxLines
    {
        get => _stateTaxLines.Count > 0
            ? _stateTaxLines
            : StateTaxLineResolver.FromTotals(
                StateWithholding, StateDisabilityInsurance, StateDisabilityInsuranceLabel);
        init => _stateTaxLines = value ?? Array.Empty<StateTaxLine>();
    }

    public decimal FicaTaxableWages { get; init; }
    public decimal SocialSecurityWithholding { get; init; }
    public decimal MedicareWithholding { get; init; }
    public decimal AdditionalMedicareWithholding { get; init; }

    public decimal FederalTaxableIncome { get; init; }
    public decimal FederalWithholding { get; init; }

    public decimal TotalTaxes => StateWithholding + StateDisabilityInsurance
                                + SocialSecurityWithholding + MedicareWithholding + AdditionalMedicareWithholding
                                + FederalWithholding;
    public decimal NetPay { get; init; }

    /// <summary>
    /// "Show Your Work" breakdown — one <see cref="LineExplanation"/> per visible
    /// paycheck line. Defaults to <see cref="PaycheckExplanation.Empty"/> when not
    /// populated. The UI uses <see cref="PaycheckExplanation.Get"/> to look up the
    /// breakdown for a specific line.
    /// </summary>
    public PaycheckExplanation Explanation { get; init; } = PaycheckExplanation.Empty;
}
