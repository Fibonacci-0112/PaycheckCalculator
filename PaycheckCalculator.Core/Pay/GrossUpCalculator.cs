using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.Core.Pay;

/// <summary>
/// Solves the inverse paycheck problem: given a desired take-home (net) amount for a
/// single pay period, find the gross pay that delivers it. This is the "gross-up"
/// employers use for net bonuses, relocation, and other payments where an exact net
/// is promised and the employer covers the resulting taxes.
///
/// Net pay is a non-decreasing function of gross (every marginal rate is well under
/// 100%), so the solver treats <see cref="PayCalculator"/> as a monotonic forward
/// function and bisects on gross. Every probe re-runs the full calculator, so
/// percentage-based deductions, FICA wage-base caps, graduated federal brackets,
/// state withholding, and W-4 extra withholding are all honored automatically.
/// </summary>
public sealed class GrossUpCalculator
{
    private readonly PayCalculator _payCalculator;

    public GrossUpCalculator(PayCalculator payCalculator)
    {
        ArgumentNullException.ThrowIfNull(payCalculator);
        _payCalculator = payCalculator;
    }

    /// <summary>
    /// Computes the gross pay required to net <paramref name="targetNetPay"/> for one
    /// pay period. All tax-relevant context (frequency, W-4, state, state inputs,
    /// deductions, YTD wages, paycheck number) is taken from <paramref name="input"/>;
    /// its gross-basis fields (<see cref="PaycheckInput.PayType"/>, hourly rate/hours,
    /// and salary amount) are ignored — the solver supplies the gross itself.
    /// </summary>
    /// <param name="input">Template input supplying the tax context for the gross-up.</param>
    /// <param name="targetNetPay">Desired net (take-home) pay for the period. Must be ≥ 0.</param>
    public GrossUpResult Calculate(PaycheckInput input, decimal targetNetPay)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (targetNetPay < 0m)
            throw new ArgumentOutOfRangeException(
                nameof(targetNetPay), targetNetPay, "Target net pay cannot be negative.");

        var target = RoundMoney(targetNetPay);

        // A zero target needs zero gross — nothing to solve.
        if (target == 0m)
            return Build(input, gross: 0m, target: 0m, converged: true);

        decimal NetAt(decimal gross) => CalculateAt(input, gross).NetPay;

        // Bracket the solution. net(gross) < gross for any positive gross (Medicare
        // alone guarantees a tax), so the target gross is a valid lower bound and we
        // grow the upper bound until its net meets the target.
        decimal low = target;
        decimal high = target * 2m;
        var guard = 0;
        while (NetAt(high) < target && guard++ < 200)
        {
            low = high;
            high *= 2m;
        }

        // Bisect to sub-cent precision. Invariant: net(low) < target ≤ net(high).
        for (var i = 0; i < 100 && high - low > 0.005m; i++)
        {
            var mid = (low + high) / 2m;
            if (NetAt(mid) < target) low = mid;
            else high = mid;
        }

        // Settle on a whole-cent gross. First make sure the rounded gross still nets
        // at least the target, then trim any excess cents so we report the smallest
        // gross that satisfies the gross-up guarantee (net ≥ target).
        var gross = RoundMoney(high);
        guard = 0;
        while (NetAt(gross) < target && guard++ < 1000)
            gross += 0.01m;
        guard = 0;
        while (gross - 0.01m >= target && NetAt(gross - 0.01m) >= target && guard++ < 1000)
            gross -= 0.01m;

        var converged = NetAt(gross) >= target;
        return Build(input, gross, target, converged);
    }

    private GrossUpResult Build(PaycheckInput input, decimal gross, decimal target, bool converged) =>
        new()
        {
            TargetNetPay = target,
            GrossUpPay = gross,
            Paycheck = CalculateAt(input, gross),
            Converged = converged
        };

    /// <summary>
    /// Runs the full paycheck calculation for a candidate gross by re-expressing the
    /// template input as a per-period salary equal to <paramref name="gross"/>,
    /// preserving every other tax-relevant field.
    /// </summary>
    private PaycheckResult CalculateAt(PaycheckInput input, decimal gross)
    {
        var probe = new PaycheckInput
        {
            Frequency = input.Frequency,
            PayType = PayType.Salary,
            SalaryBasis = SalaryBasis.PerPeriod,
            SalaryAmount = gross,
            State = input.State,
            StateInputValues = input.StateInputValues,
            FederalW4 = input.FederalW4,
            Deductions = input.Deductions,
            YtdSocialSecurityWages = input.YtdSocialSecurityWages,
            YtdMedicareWages = input.YtdMedicareWages,
            PaycheckNumber = input.PaycheckNumber
        };
        return _payCalculator.Calculate(probe);
    }

    private static decimal RoundMoney(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
