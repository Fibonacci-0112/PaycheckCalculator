namespace PaycheckCalc.Core.Tax.Supplemental;

/// <summary>
/// How a state withholds income tax on supplemental wages (bonuses, commissions, etc.).
/// </summary>
public enum StateSupplementalMethod
{
    /// <summary>The state imposes no income tax on wages — nothing is withheld.</summary>
    NoIncomeTax,

    /// <summary>A flat percentage applied directly to the supplemental payment.</summary>
    FlatRate,

    /// <summary>
    /// A percentage of the <b>federal</b> supplemental withholding on the payment
    /// (used by Vermont, which withholds 30% of the federal amount).
    /// </summary>
    FederalPercentage,

    /// <summary>
    /// The state publishes no separate flat supplemental rate; employers must use the
    /// aggregate / regular withholding method. This calculator does not estimate it.
    /// </summary>
    RegularMethod
}
