using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Models;

public sealed class PaycheckInput
{
    public PayFrequency Frequency { get; init; }

    public decimal HourlyRate { get; init; }
    public decimal RegularHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal OvertimeMultiplier { get; init; } = 1.5m;

    public UsState State { get; init; } = UsState.OK;

    /// <summary>
    /// Dynamic state-specific input values populated by the UI from the
    /// state's schema (see <see cref="IStateSchemaProvider.GetSchema"/>).
    /// </summary>
    public StateInputValues? StateInputValues { get; init; }

    public FederalW4Input FederalW4 { get; init; } = new();

    public IReadOnlyList<Deduction> Deductions { get; init; } = Array.Empty<Deduction>();
     
    public decimal YtdSocialSecurityWages { get; init; } = 0m;
    public decimal YtdMedicareWages { get; init; } = 0m;

    /// <summary>
    /// 1-based paycheck number within the current year (e.g. 1 for the first paycheck).
    /// Used by the annual projection calculator to compute projected YTD and remaining paychecks.
    /// Defaults to 1 when not specified.
    /// </summary>
    public int PaycheckNumber { get; init; } = 1;
}
