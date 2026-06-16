namespace PaycheckCalc.Core.Models;

public enum PayFrequency { Weekly, Biweekly, Semimonthly, Monthly, Quarterly, Semiannual, Annual, Daily, Weekly53, Biweekly27 }
public enum FilingStatus { Single, Married }
public enum DeductionType { PreTax, PostTax }
public enum DeductionAmountType { Dollar, Percentage }

/// <summary>How a paycheck's gross pay is specified.</summary>
public enum PayType
{
    /// <summary>Gross pay is derived from hours worked × hourly rate (+ overtime).</summary>
    Hourly,

    /// <summary>Gross pay is derived from a salary amount (see <see cref="SalaryBasis"/>).</summary>
    Salary
}

/// <summary>Direction of an hourly ↔ salary conversion (see <c>HourlySalaryCalculator</c>).</summary>
public enum PayConversionMode
{
    /// <summary>Given an hourly rate, derive the equivalent salary and per-period pay.</summary>
    HourlyToSalary,

    /// <summary>Given an annual salary, derive the equivalent ("real") hourly rate.</summary>
    SalaryToHourly
}

/// <summary>How a salary amount maps onto a single pay period.</summary>
public enum SalaryBasis
{
    /// <summary>The amount is an annual salary; per-period gross = amount ÷ pay periods per year.</summary>
    PerYear,

    /// <summary>The amount is already the gross pay for a single pay period.</summary>
    PerPeriod
}
