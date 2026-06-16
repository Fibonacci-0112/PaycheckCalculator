namespace PaycheckCalc.Core.Models;

/// <summary>
/// Result of an hourly ↔ salary conversion: the equivalent hourly rate and annual
/// salary, plus the same earnings expressed across pay frequencies. All money values
/// are rounded to the cent; the per-frequency amounts are each derived from the
/// unrounded annual salary (so they need not sum back to it exactly).
/// </summary>
public sealed class HourlySalaryResult
{
    /// <summary>The conversion direction that produced this result.</summary>
    public PayConversionMode Mode { get; init; }

    /// <summary>The equivalent hourly rate, rounded to the cent.</summary>
    public decimal HourlyRate { get; init; }

    /// <summary>The equivalent annual (gross) salary, rounded to the cent.</summary>
    public decimal AnnualSalary { get; init; }

    /// <summary>Hours worked per week used for the conversion.</summary>
    public decimal HoursPerWeek { get; init; }

    /// <summary>Paid weeks per year used for the conversion.</summary>
    public decimal WeeksPerYear { get; init; }

    /// <summary>Total hours worked per year (<see cref="HoursPerWeek"/> × <see cref="WeeksPerYear"/>).</summary>
    public decimal HoursPerYear { get; init; }

    /// <summary>The frequency reported by <see cref="PerPeriodPay"/>.</summary>
    public PayFrequency Frequency { get; init; }

    /// <summary>Gross pay for one period of <see cref="Frequency"/>, rounded to the cent.</summary>
    public decimal PerPeriodPay { get; init; }

    /// <summary>Gross pay for a single week (annual ÷ 52), rounded to the cent.</summary>
    public decimal WeeklyPay { get; init; }

    /// <summary>Gross pay for a biweekly period (annual ÷ 26), rounded to the cent.</summary>
    public decimal BiweeklyPay { get; init; }

    /// <summary>Gross pay for a semimonthly period (annual ÷ 24), rounded to the cent.</summary>
    public decimal SemimonthlyPay { get; init; }

    /// <summary>Gross pay for a single month (annual ÷ 12), rounded to the cent.</summary>
    public decimal MonthlyPay { get; init; }
}
