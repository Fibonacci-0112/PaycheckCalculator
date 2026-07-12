using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Core.Models;

public sealed class PaycheckInput
{
    public PayFrequency Frequency { get; init; }

    /// <summary>
    /// Whether gross pay is specified as an hourly rate × hours (default) or as a salary.
    /// </summary>
    public PayType PayType { get; init; } = PayType.Hourly;

    // ── Hourly inputs (used when <see cref="PayType"/> is <see cref="PayType.Hourly"/>) ──
    public decimal HourlyRate { get; init; }
    public decimal RegularHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal OvertimeMultiplier { get; init; } = 1.5m;

    // ── Salary inputs (used when <see cref="PayType"/> is <see cref="PayType.Salary"/>) ──
    /// <summary>
    /// The salary amount. Interpreted per <see cref="SalaryBasis"/>: an annual figure
    /// (<see cref="SalaryBasis.PerYear"/>) or the gross for a single period
    /// (<see cref="SalaryBasis.PerPeriod"/>). Ignored when <see cref="PayType"/> is hourly.
    /// </summary>
    public decimal SalaryAmount { get; init; }

    /// <summary>How <see cref="SalaryAmount"/> maps onto a single pay period.</summary>
    public SalaryBasis SalaryBasis { get; init; } = SalaryBasis.PerYear;

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
