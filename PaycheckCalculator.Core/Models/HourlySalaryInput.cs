namespace PaycheckCalculator.Core.Models;

/// <summary>
/// Inputs for an hourly ↔ salary conversion. This is a pure pay-rate conversion (no
/// taxes or deductions): it answers "what salary does this hourly rate work out to?"
/// and "what's my real hourly rate?" given the hours actually worked, and breaks the
/// equivalent earnings out across pay frequencies.
/// </summary>
public sealed class HourlySalaryInput
{
    /// <summary>Which direction to convert. Defaults to hourly → salary.</summary>
    public PayConversionMode Mode { get; init; } = PayConversionMode.HourlyToSalary;

    /// <summary>
    /// The hourly rate to convert from. Used when <see cref="Mode"/> is
    /// <see cref="PayConversionMode.HourlyToSalary"/>; ignored otherwise.
    /// </summary>
    public decimal HourlyRate { get; init; }

    /// <summary>
    /// The annual salary to convert from. Used when <see cref="Mode"/> is
    /// <see cref="PayConversionMode.SalaryToHourly"/>; ignored otherwise.
    /// </summary>
    public decimal AnnualSalary { get; init; }

    /// <summary>
    /// Hours worked per week. Drives both the hourly → annual multiplier and the "real
    /// hourly" divisor (set this to the hours actually worked — e.g. 50 — to see the true
    /// hourly rate behind a salary). Defaults to a 40-hour week. Must be greater than 0.
    /// </summary>
    public decimal HoursPerWeek { get; init; } = 40m;

    /// <summary>
    /// Paid weeks per year. Lower this (e.g. 50) to account for unpaid time off. Defaults
    /// to 52. Must be greater than 0.
    /// </summary>
    public decimal WeeksPerYear { get; init; } = 52m;

    /// <summary>
    /// The pay frequency whose per-period amount is reported in
    /// <see cref="HourlySalaryResult.PerPeriodPay"/>. Defaults to biweekly. The annual,
    /// weekly, biweekly, semimonthly, and monthly equivalents are always reported.
    /// </summary>
    public PayFrequency Frequency { get; init; } = PayFrequency.Biweekly;
}
