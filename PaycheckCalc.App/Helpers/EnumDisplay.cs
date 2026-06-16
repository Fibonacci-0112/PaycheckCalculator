using System;
using System.Collections.Generic;
using System.Text;

namespace PaycheckCalc.App.Helpers;

public static class EnumDisplay
{
    public static string DeductionType(string name) => name switch
    {
        "PreTax" => "Pre-Tax",
        "PostTax" => "Post-Tax",
        _ => SplitPascalCase(name)
    };

    public static string PayFrequency(string name) => name switch
    {
        "Biweekly" => "Bi-Weekly",
        "Semimonthly" => "Semi-Monthly",
        "Semiannual" => "Semi-Annual",
        "Weekly53" => "Weekly 53",
        "Biweekly27" => "Bi-Weekly 27",
        _ => SplitPascalCase(name)
    };

    public static string PayType(string name) => name switch
    {
        "Hourly" => "Hourly",
        "Salary" => "Salary",
        _ => SplitPascalCase(name)
    };

    public static string CalculationMode(string name) => name switch
    {
        "Standard" => "Standard Paycheck",
        "GrossUp" => "Gross-Up (net → gross)",
        "Bonus" => "Bonus / Supplemental Wage",
        _ => SplitPascalCase(name)
    };

    public static string SalaryBasis(string name) => name switch
    {
        "PerYear" => "Per Year",
        "PerPeriod" => "Pay Per Period",
        _ => SplitPascalCase(name)
    };

    public static string FederalFilingStatus(string name) => name switch
    {
        "SingleOrMarriedSeparately" => "Single, Married Filing Separately",
        "MarriedFilingJointly" => "Married Filing Jointly, Qualifying Surviving Spouse",
        "HeadOfHousehold" => "Head of Household",
        _ => SplitPascalCase(name)
    };

    public static string UsStateName(string abbreviation) => abbreviation switch
    {
        "AL" => "Alabama",
        "AK" => "Alaska",
        "AZ" => "Arizona",
        "AR" => "Arkansas",
        "CA" => "California",
        "CO" => "Colorado",
        "CT" => "Connecticut",
        "DC" => "District of Columbia",
        "DE" => "Delaware",
        "FL" => "Florida",
        "GA" => "Georgia",
        "HI" => "Hawaii",
        "ID" => "Idaho",
        "IL" => "Illinois",
        "IN" => "Indiana",
        "IA" => "Iowa",
        "KS" => "Kansas",
        "KY" => "Kentucky",
        "LA" => "Louisiana",
        "ME" => "Maine",
        "MD" => "Maryland",
        "MA" => "Massachusetts",
        "MI" => "Michigan",
        "MN" => "Minnesota",
        "MS" => "Mississippi",
        "MO" => "Missouri",
        "MT" => "Montana",
        "NE" => "Nebraska",
        "NV" => "Nevada",
        "NH" => "New Hampshire",
        "NJ" => "New Jersey",
        "NM" => "New Mexico",
        "NY" => "New York",
        "NC" => "North Carolina",
        "ND" => "North Dakota",
        "OH" => "Ohio",
        "OK" => "Oklahoma",
        "OR" => "Oregon",
        "PA" => "Pennsylvania",
        "RI" => "Rhode Island",
        "SC" => "South Carolina",
        "SD" => "South Dakota",
        "TN" => "Tennessee",
        "TX" => "Texas",
        "UT" => "Utah",
        "VT" => "Vermont",
        "VA" => "Virginia",
        "WA" => "Washington",
        "WV" => "West Virginia",
        "WI" => "Wisconsin",
        "WY" => "Wyoming",
        _ => abbreviation
    };

    private static string SplitPascalCase(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        var chars = new List<char>(s.Length + 8);

        for (int i = 0;i < s.Length;i++)
        {
            var c = s[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(s[i - 1]))
                chars.Add(' ');
            chars.Add(c);
        }
        return new string(chars.ToArray());
    }
}