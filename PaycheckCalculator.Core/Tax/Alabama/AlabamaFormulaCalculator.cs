using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace PaycheckCalculator.Core.Tax.Alabama
{
    public enum AlabamaFilingStatus
    {
        Zero, // "0"
        Single, // "S"
        MarriedFilingJointly, // "M"
        MarriedFilingSeparately, // "MS"
        HeadOfFamily, // "H"
    }

    public class AlabamaFormulaCalculator
    {
        public static decimal CalculateWithholding(
            decimal grossWagesPerPeriod,
            int payPeriodsPerYear,
            decimal federalWithholdingPerPeriod,
            AlabamaFilingStatus filingStatus,
            int dependents)
        {
            // Step 1 - Annualize Gross Income (GI)
            decimal annualGrossIncome = grossWagesPerPeriod * payPeriodsPerYear;

            // Step 2A - Standard Deduction
            decimal standardDeduction = GetStandardDeduction(annualGrossIncome, filingStatus);

            // Step 2B - Annualize Federal Withholding
            decimal annualFederalWithholding = federalWithholdingPerPeriod * payPeriodsPerYear;

            // Step 2C - Personal Exemption
            decimal personalExemption = GetPersonalExemption(filingStatus);

            // Step 2D - Dependent Deduction
            decimal dependentDeduction;

            if (annualGrossIncome <= 50000)
                dependentDeduction = dependents * 1000m;
            else if (annualGrossIncome >= 50000 && annualGrossIncome <= 100000)
                dependentDeduction = dependents * 500m;
            else
                dependentDeduction = dependents * 300m;

            // Step 3 - Total Deductions
            decimal totalDeductions =
                standardDeduction +
                annualFederalWithholding +
                personalExemption +
                dependentDeduction;

            // Step 4 - Taxable Income
            decimal taxableIncome = annualGrossIncome - totalDeductions;

            // Step 5 - Use Tax Brackets to Calculate Annual Tax
            decimal annualTax = CalculateAnnualTax(taxableIncome, filingStatus);

            // Step 6 - Convert to Withholding Per Pay Period
            decimal withholdingPerPeriod = annualTax / payPeriodsPerYear;

            return Math.Round(withholdingPerPeriod, 2);
        }
        private static decimal GetStandardDeduction(decimal gi, AlabamaFilingStatus status)
        {
            switch (status)
            {
                case AlabamaFilingStatus.Zero:
                case AlabamaFilingStatus.Single:
                    if (gi <= 25999) return 3000;
                    if (gi < 35500)
                    {
                        decimal increments = Math.Ceiling((gi - 25999) / 500m);
                        return 3000 - (increments * 25);
                    }
                    return 2500;

                case AlabamaFilingStatus.MarriedFilingSeparately:
                    if (gi <= 12999) return 4250;
                    if (gi < 17750)
                    {
                        decimal increments = Math.Ceiling((gi - 12999) / 250m);
                        return 4250 - (increments * 88);
                    }
                    return 2500;

                case AlabamaFilingStatus.MarriedFilingJointly:
                    if (gi <= 25999) return 8500;
                    if (gi < 35500)
                    {
                        decimal increments = Math.Ceiling((gi - 25999) / 500m);
                        return 8500 - (increments * 175);
                    }
                    return 5000;

                case AlabamaFilingStatus.HeadOfFamily:
                    if (gi <= 25999) return 5200;
                    if (gi < 35500)
                    {
                        decimal increments = Math.Ceiling((gi - 25999) / 500m);
                        return 5200 - (increments * 135);
                    }
                    return 2500;

                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        private static decimal GetPersonalExemption(AlabamaFilingStatus filingStatus)
        {
            return filingStatus switch
            {
                AlabamaFilingStatus.Zero => 0m,
                AlabamaFilingStatus.Single => 1500m,
                AlabamaFilingStatus.MarriedFilingSeparately => 1500m,
                AlabamaFilingStatus.MarriedFilingJointly => 3000m,
                AlabamaFilingStatus.HeadOfFamily => 3000m,
                _ => throw new ArgumentOutOfRangeException(nameof(filingStatus)),
            };
        }

        private static decimal CalculateAnnualTax(decimal taxableIncome, AlabamaFilingStatus filingStatus)
        {
            decimal firstBracket;
            decimal secondBracket;

            // Different first/second brackets for "M"
            if (filingStatus == AlabamaFilingStatus.MarriedFilingJointly)
            {
                firstBracket = 1000m;
                secondBracket = 5000m;
            }
            else
            {
                firstBracket = 500m;
                secondBracket = 2500m;
            }

            decimal tax = 0m;

            // 2%
            decimal amountAt2 = Math.Min(taxableIncome, firstBracket);
            tax += amountAt2 * 0.02m;

            // 4%
            decimal amountAt4 = Math.Min(Math.Max(taxableIncome - firstBracket, 0), secondBracket);
            tax += amountAt4 * 0.04m;

            // 5%
            decimal amountAt5 = Math.Max(taxableIncome - firstBracket - secondBracket, 0);
            tax += amountAt5 * 0.05m;

            return tax;
        }
    }
}
