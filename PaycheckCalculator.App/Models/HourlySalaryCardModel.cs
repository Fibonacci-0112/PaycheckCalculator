using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.App.Models;

/// <summary>
/// Presentation model for the hourly ↔ salary rate converter. It is deliberately separate from
/// <see cref="ResultCardModel"/>: a rate conversion has no taxes, deductions, or net pay, so it
/// must not be displayed through the paycheck result cards.
/// </summary>
public sealed class HourlySalaryCardModel
{
    /// <summary>The conversion direction that produced this result.</summary>
    public PayConversionMode Mode { get; init; }

    /// <summary>The equivalent hourly rate.</summary>
    public decimal HourlyRate { get; init; }

    /// <summary>The equivalent annual (gross) salary.</summary>
    public decimal AnnualSalary { get; init; }

    /// <summary>Hours worked per week used for the conversion.</summary>
    public decimal HoursPerWeek { get; init; }

    /// <summary>Paid weeks per year used for the conversion.</summary>
    public decimal WeeksPerYear { get; init; }

    /// <summary>Total hours worked per year.</summary>
    public decimal HoursPerYear { get; init; }

    /// <summary>Gross pay for one period of the selected pay frequency.</summary>
    public decimal PerPeriodPay { get; init; }

    /// <summary>Display label for the selected pay frequency (e.g. "Bi-Weekly").</summary>
    public string FrequencyLabel { get; init; } = "";

    /// <summary>Gross pay for a single week.</summary>
    public decimal WeeklyPay { get; init; }

    /// <summary>Gross pay for a biweekly period.</summary>
    public decimal BiweeklyPay { get; init; }

    /// <summary>Gross pay for a semimonthly period.</summary>
    public decimal SemimonthlyPay { get; init; }

    /// <summary>Gross pay for a single month.</summary>
    public decimal MonthlyPay { get; init; }

    /// <summary>Caption for the figure the user asked the converter to produce.</summary>
    public string ComputedLabel =>
        Mode == PayConversionMode.HourlyToSalary ? "Equivalent Annual Salary" : "Real Hourly Rate";

    /// <summary>The figure the user asked the converter to produce.</summary>
    public decimal ComputedAmount =>
        Mode == PayConversionMode.HourlyToSalary ? AnnualSalary : HourlyRate;

    /// <summary>Caption for the figure the user supplied.</summary>
    public string SourceLabel =>
        Mode == PayConversionMode.HourlyToSalary ? "Hourly Rate" : "Annual Salary";

    /// <summary>The figure the user supplied.</summary>
    public decimal SourceAmount =>
        Mode == PayConversionMode.HourlyToSalary ? HourlyRate : AnnualSalary;
}
