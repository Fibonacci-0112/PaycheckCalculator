using System.Text.Json;
using System.Text.Json.Serialization;
using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Core.Tax.Maryland;

/// <summary>
/// Maryland's county income tax rates, loaded from
/// <c>md_county_rates_2026.json</c>.
/// <para>
/// Unlike the optional local taxes other states levy, every Maryland employee
/// pays a county rate, applied to the same taxable income the state schedule
/// uses. Most counties charge a single rate; Anne Arundel and Frederick apply
/// graduated marginal brackets that differ by filing status.
/// </para>
/// <para>
/// The county's actual rate is used, not the rounded-up rate group the
/// Comptroller's printed tables bucket it into — those groups exist so manual
/// filers have ten tables to look up instead of twenty-four.
/// </para>
/// </summary>
public sealed class MarylandCountyRates
{
    private readonly Dictionary<string, CountyRate> _byName;

    private MarylandCountyRates(Dictionary<string, CountyRate> byName, string defaultCounty, IReadOnlyList<string> names)
    {
        _byName = byName;
        DefaultCounty = defaultCounty;
        CountyNames = names;
    }

    /// <summary>
    /// The county used when the employee has not reported one. The Comptroller
    /// directs employers to withhold at the highest local rate in that case.
    /// </summary>
    public string DefaultCounty { get; }

    /// <summary>Every selectable county, in the order the schema lists them.</summary>
    public IReadOnlyList<string> CountyNames { get; }

    /// <summary>Parses the rate table. Throws when the JSON is malformed.</summary>
    public static MarylandCountyRates Load(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var root = JsonSerializer.Deserialize<CountyRoot>(json, options)
            ?? throw new InvalidOperationException("Maryland county rate data could not be parsed.");

        if (root.Counties.Count == 0)
            throw new InvalidOperationException("Maryland county rate data contains no counties.");

        var byName = new Dictionary<string, CountyRate>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        foreach (var dto in root.Counties)
        {
            var rate = dto.ToRate();
            byName[rate.Name] = rate;
            names.Add(rate.Name);
        }

        if (!byName.ContainsKey(root.DefaultCounty))
            throw new InvalidOperationException($"Maryland default county '{root.DefaultCounty}' is not in the rate table.");

        return new MarylandCountyRates(byName, root.DefaultCounty, names);
    }

    /// <summary>Whether <paramref name="county"/> is a known selection.</summary>
    public bool IsKnown(string? county) => county is not null && _byName.ContainsKey(county);

    /// <summary>
    /// Computes one payroll period's county income tax on
    /// <paramref name="taxableIncome"/> and the worksheet behind it. Falls back to
    /// <see cref="DefaultCounty"/> — the Comptroller's own rule — when the
    /// selection is missing or unrecognized.
    /// </summary>
    /// <param name="county">The county of residence reported on Form MW507.</param>
    /// <param name="taxableIncome">Maryland taxable income for this payroll period.</param>
    /// <param name="bracketDivisor">
    /// Divisor that scales the graduated counties' annual bracket ceilings down to
    /// the payroll period, matching how the state schedule is scaled. Ignored by
    /// the flat-rate counties, whose rate is the same at any income.
    /// </param>
    /// <param name="isMarriedOrHeadOfHousehold">Selects the joint bracket set.</param>
    public MarylandCountyTax Calculate(
        string? county, decimal taxableIncome, int bracketDivisor, bool isMarriedOrHeadOfHousehold)
    {
        var rate = _byName.TryGetValue(county ?? "", out var found) ? found : _byName[DefaultCounty];
        var income = Math.Max(0m, taxableIncome);
        var steps = new List<ExplanationStep>();

        if (rate.Brackets is null)
        {
            var flat = income * rate.FlatRate;
            steps.Add(new ExplanationStep(
                $"{rate.Name} local rate",
                "Maryland county income tax applies to the same taxable income as the state schedule.",
                flat,
                $"{Money(income)} × {Percent(rate.FlatRate)} = {Money(flat)}"));
            return new MarylandCountyTax(rate.Name, flat, steps);
        }

        var brackets = isMarriedOrHeadOfHousehold ? rate.Brackets.Joint : rate.Brackets.Single;
        var tax = 0m;
        var lower = 0m;

        foreach (var bracket in brackets)
        {
            if (income <= lower)
                break;

            var upper = bracket.UpTo is { } annualCeiling
                ? annualCeiling / bracketDivisor
                : decimal.MaxValue;

            var slice = Math.Min(income, upper) - lower;
            var portion = slice * bracket.Rate;
            tax += portion;

            steps.Add(new ExplanationStep(
                bracket.UpTo is null
                    ? $"{rate.Name} local rate over {Money(lower)}"
                    : $"{rate.Name} local rate {Money(lower)} to {Money(upper)}",
                "This county applies graduated local rates, so each slice of income is taxed at its own rate.",
                portion,
                $"{Money(slice)} × {Percent(bracket.Rate)} = {Money(portion)}"));

            lower = upper;
        }

        return new MarylandCountyTax(rate.Name, tax, steps);
    }

    private static string Money(decimal value) => StateExplanationSteps.Money(value);
    private static string Percent(decimal rate) => StateExplanationSteps.Percent(rate);

    private sealed record CountyRate(
        string Name,
        decimal FlatRate,
        CountyBrackets? Brackets);

    private sealed record CountyBrackets(
        IReadOnlyList<CountyBracket> Single,
        IReadOnlyList<CountyBracket> Joint);

    private sealed record CountyBracket(decimal? UpTo, decimal Rate);

    private sealed class CountyRoot
    {
        [JsonPropertyName("defaultCounty")] public string DefaultCounty { get; set; } = "";
        [JsonPropertyName("counties")] public List<CountyDto> Counties { get; set; } = new();
    }

    private sealed class CountyDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("rate")] public decimal? Rate { get; set; }
        [JsonPropertyName("brackets")] public BracketSetDto? Brackets { get; set; }

        public CountyRate ToRate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("Every Maryland county entry requires a name.");

            if (Brackets is not null)
            {
                return new CountyRate(Name, 0m, new CountyBrackets(
                    Convert(Brackets.Single, Name, "single"),
                    Convert(Brackets.Joint, Name, "joint")));
            }

            if (Rate is not { } flat || flat <= 0m)
                throw new InvalidOperationException($"Maryland county '{Name}' requires a positive rate or graduated brackets.");

            return new CountyRate(Name, flat, null);
        }

        private static IReadOnlyList<CountyBracket> Convert(
            List<BracketDto>? brackets, string county, string status)
        {
            if (brackets is null || brackets.Count == 0)
                throw new InvalidOperationException($"Maryland county '{county}' is missing its {status} brackets.");

            return brackets.Select(b => new CountyBracket(b.UpTo, b.Rate)).ToList();
        }
    }

    private sealed class BracketSetDto
    {
        [JsonPropertyName("single")] public List<BracketDto>? Single { get; set; }
        [JsonPropertyName("joint")] public List<BracketDto>? Joint { get; set; }
    }

    private sealed class BracketDto
    {
        [JsonPropertyName("upTo")] public decimal? UpTo { get; set; }
        [JsonPropertyName("rate")] public decimal Rate { get; set; }
    }
}

/// <summary>One payroll period's county income tax, plus the steps that produced it.</summary>
/// <param name="CountyName">The county actually used, after any fallback.</param>
/// <param name="Tax">County tax on the period's taxable income, unrounded.</param>
/// <param name="Steps">Worksheet steps for the "Show Your Work" breakdown.</param>
public sealed record MarylandCountyTax(
    string CountyName,
    decimal Tax,
    IReadOnlyList<ExplanationStep> Steps);
