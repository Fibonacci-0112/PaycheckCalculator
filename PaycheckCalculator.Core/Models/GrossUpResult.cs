namespace PaycheckCalculator.Core.Models;

/// <summary>
/// Result of a gross-up calculation: the gross pay required to deliver a desired
/// take-home (net) amount for a single pay period, together with the full per-period
/// breakdown computed at that gross.
/// </summary>
public sealed class GrossUpResult
{
    /// <summary>The desired net (take-home) pay the gross-up targets, rounded to the cent.</summary>
    public decimal TargetNetPay { get; init; }

    /// <summary>The gross pay needed to deliver <see cref="TargetNetPay"/>, rounded to the cent.</summary>
    public decimal GrossUpPay { get; init; }

    /// <summary>
    /// The full per-period paycheck breakdown computed at <see cref="GrossUpPay"/>
    /// (taxes, deductions, and the resulting net). Defaults to an empty result.
    /// </summary>
    public PaycheckResult Paycheck { get; init; } = new();

    /// <summary>
    /// Actual net pay produced at <see cref="GrossUpPay"/>. Equals (or, because of
    /// cent rounding, very slightly exceeds) <see cref="TargetNetPay"/> when the
    /// solver converged.
    /// </summary>
    public decimal ActualNetPay => Paycheck.NetPay;

    /// <summary>
    /// The extra gross beyond the target net that covers taxes and deductions
    /// (<see cref="GrossUpPay"/> − <see cref="TargetNetPay"/>).
    /// </summary>
    public decimal GrossUpCost => GrossUpPay - TargetNetPay;

    /// <summary>
    /// True when the solver found a gross whose resulting net meets or exceeds the
    /// target. False only in pathological cases where no gross could satisfy the
    /// target (e.g. fixed extra withholding larger than any reachable net).
    /// </summary>
    public bool Converged { get; init; }
}
