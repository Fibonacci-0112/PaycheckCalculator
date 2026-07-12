using System.Text.Json.Serialization;

namespace PaycheckCalculator.Core.Tax.Federal;

internal sealed class Irs15TRoot
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("annualTables")]
    public AnnualTables AnnualTables { get; set; } = new();

    [JsonPropertyName("worksheetConstants")]
    public WorksheetConstants WorksheetConstants { get; set; } = new();
}

internal sealed class AnnualTables
{
    [JsonPropertyName("standard")]
    public AnnualSchedule Standard { get; set; } = new();

    [JsonPropertyName("step2_checked")]
    public AnnualSchedule Step2Checked { get; set; } = new();
}

internal sealed class AnnualSchedule
{
    [JsonPropertyName("married_filing_jointly")]
    public List<AnnualBracket> MarriedFilingJointly { get; set; } = new();

    [JsonPropertyName("single_or_mfs")]
    public List<AnnualBracket> SingleOrMfs { get; set; } = new();

    [JsonPropertyName("head_of_household")]
    public List<AnnualBracket> HeadOfHousehold { get; set; } = new();
}

internal sealed class WorksheetConstants
{
    [JsonPropertyName("line1g")]
    public Line1GConstants Line1G { get; set; } = new();
}

internal sealed class Line1GConstants
{
    [JsonPropertyName("mfj")]
    public decimal Mfj { get; set; }
    [JsonPropertyName("other")]
    public decimal Other { get; set; }
}

internal sealed class AnnualBracket
{
    [JsonPropertyName("over")]
    public decimal Over { get; set; }

    [JsonPropertyName("under")]
    public decimal? Under { get; set; }

    [JsonPropertyName("base")]
    public decimal Base { get; set; }

    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }

    [JsonPropertyName("excessOver")]
    public decimal ExcessOver { get; set; }
}
