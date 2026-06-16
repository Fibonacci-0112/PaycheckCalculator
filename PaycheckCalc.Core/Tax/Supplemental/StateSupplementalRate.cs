namespace PaycheckCalc.Core.Tax.Supplemental;

/// <summary>
/// A single state's supplemental-wage withholding rule, deserialized from
/// <c>state_supplemental_2026.json</c>.
/// </summary>
public sealed class StateSupplementalRate
{
    /// <summary>How the state withholds on supplemental wages.</summary>
    public StateSupplementalMethod Method { get; init; }

    /// <summary>
    /// The rate, interpreted per <see cref="Method"/>: a fraction of the payment for
    /// <see cref="StateSupplementalMethod.FlatRate"/>, or a fraction of the federal
    /// supplemental withholding for <see cref="StateSupplementalMethod.FederalPercentage"/>.
    /// Ignored for the other methods.
    /// </summary>
    public decimal Rate { get; init; }

    /// <summary>Optional human-readable caveat shown alongside the result (e.g. local taxes excluded).</summary>
    public string? Note { get; init; }
}
