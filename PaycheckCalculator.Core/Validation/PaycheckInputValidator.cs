namespace PaycheckCalculator.Core.Validation;

/// <summary>
/// Sanity-checks calculation inputs for negative, blank/zero-where-invalid, and implausible
/// out-of-range values before they are run through a calculator. This does not encode tax
/// law — it exists so both front-ends (MAUI and Blazor) share one authoritative source of
/// "this input can't possibly be right" checks instead of duplicating ad hoc validation, and
/// so a bad value surfaces as a clear message rather than a silently wrong (or crashing)
/// calculation.
/// </summary>
public static class PaycheckInputValidator
{
    /// <summary>
    /// Maximum plausible hours (regular + overtime combined) in a single pay period —
    /// generous enough to cover every <see cref="Models.PayFrequency"/> (even Monthly)
    /// without rejecting legitimate entries: 31 days × 24 hours.
    /// </summary>
    public const decimal MaxHoursPerPeriod = 744m;

    /// <summary>1-based paycheck number bounds — supports up to daily pay frequency.</summary>
    public const int MaxPaycheckNumber = 366;

    /// <summary>
    /// Maximum plausible hours worked in a single week for the hourly ↔ salary converter:
    /// 7 days × 24 hours.
    /// </summary>
    public const decimal MaxHoursPerWeek = 168m;

    /// <summary>Maximum plausible paid weeks per year for the hourly ↔ salary converter.</summary>
    public const decimal MaxWeeksPerYear = 53m;

    /// <summary>
    /// Validates a standard paycheck (or gross-up) input. Pass <paramref name="targetNetPay"/>
    /// when validating a gross-up calculation so the desired net pay is checked too.
    /// </summary>
    public static IReadOnlyList<string> Validate(Models.PaycheckInput input, decimal? targetNetPay = null)
    {
        var errors = new List<string>();

        if (input.PayType == Models.PayType.Hourly)
        {
            if (input.HourlyRate < 0)
                errors.Add("Hourly rate cannot be negative.");
            if (input.RegularHours < 0)
                errors.Add("Regular hours cannot be negative.");
            if (input.OvertimeHours < 0)
                errors.Add("Overtime hours cannot be negative.");
            if (input.RegularHours + input.OvertimeHours > MaxHoursPerPeriod)
                errors.Add("Regular + overtime hours exceed what's possible in a single pay period.");
            if (input.OvertimeMultiplier < 1m)
                errors.Add("Overtime multiplier must be at least 1.0.");
        }
        else if (input.SalaryAmount < 0)
        {
            errors.Add("Salary / gross pay amount cannot be negative.");
        }

        if (input.YtdSocialSecurityWages < 0)
            errors.Add("YTD Social Security wages cannot be negative.");
        if (input.YtdMedicareWages < 0)
            errors.Add("YTD Medicare wages cannot be negative.");

        if (input.PaycheckNumber < 1 || input.PaycheckNumber > MaxPaycheckNumber)
            errors.Add($"Paycheck number must be between 1 and {MaxPaycheckNumber}.");

        if (targetNetPay is <= 0)
            errors.Add("Desired net pay must be greater than zero.");

        return errors;
    }

    /// <summary>Validates a supplemental-wage (bonus) input.</summary>
    public static IReadOnlyList<string> ValidateBonus(Models.BonusInput input)
    {
        var errors = new List<string>();

        if (input.BonusAmount <= 0)
            errors.Add("Bonus amount must be greater than zero.");
        if (input.YtdSupplementalWages < 0)
            errors.Add("Prior supplemental wages this year cannot be negative.");
        if (input.YtdSocialSecurityWages < 0)
            errors.Add("YTD Social Security wages cannot be negative.");
        if (input.YtdMedicareWages < 0)
            errors.Add("YTD Medicare wages cannot be negative.");

        return errors;
    }

    /// <summary>Validates a self-employment / 1099 input.</summary>
    public static IReadOnlyList<string> ValidateSelfEmployment(Models.SelfEmploymentInput input)
    {
        var errors = new List<string>();

        if (input.AnnualNetEarnings < 0)
            errors.Add("Annual net self-employment earnings cannot be negative.");
        if (input.YtdSocialSecurityWages < 0)
            errors.Add("YTD Social Security wages cannot be negative.");
        if (input.YtdMedicareWages < 0)
            errors.Add("YTD Medicare wages cannot be negative.");

        return errors;
    }

    /// <summary>
    /// Validates an hourly ↔ salary conversion input. Only the amount for the selected
    /// direction is checked — the other one is ignored by the converter.
    /// </summary>
    public static IReadOnlyList<string> ValidateHourlySalary(Models.HourlySalaryInput input)
    {
        var errors = new List<string>();

        if (input.HoursPerWeek <= 0)
            errors.Add("Hours per week must be greater than zero.");
        else if (input.HoursPerWeek > MaxHoursPerWeek)
            errors.Add($"Hours per week cannot exceed {MaxHoursPerWeek}.");

        if (input.WeeksPerYear <= 0)
            errors.Add("Paid weeks per year must be greater than zero.");
        else if (input.WeeksPerYear > MaxWeeksPerYear)
            errors.Add($"Paid weeks per year cannot exceed {MaxWeeksPerYear}.");

        if (input.Mode == Models.PayConversionMode.HourlyToSalary)
        {
            if (input.HourlyRate <= 0)
                errors.Add("Hourly rate must be greater than zero.");
        }
        else if (input.AnnualSalary <= 0)
        {
            errors.Add("Annual salary must be greater than zero.");
        }

        return errors;
    }
}
