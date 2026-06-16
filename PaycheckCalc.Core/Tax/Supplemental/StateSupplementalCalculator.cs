using System.Text.Json;
using System.Text.Json.Serialization;
using PaycheckCalc.Core.Explanation;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.Supplemental;

/// <summary>
/// State income-tax withholding on supplemental wages (bonuses, commissions, etc.),
/// driven by the data table in <c>state_supplemental_2026.json</c>.
///
/// Each state resolves to one of four methods (see <see cref="StateSupplementalMethod"/>):
/// no income tax, a flat percentage of the payment, a percentage of the federal
/// supplemental withholding (Vermont), or the regular/aggregate method — which this
/// flat-rate calculator does not estimate and instead flags for the caller.
/// </summary>
public sealed class StateSupplementalCalculator
{
    private readonly IReadOnlyDictionary<UsState, StateSupplementalRate> _rates;

    /// <summary>The tax year the loaded supplemental-rate table applies to.</summary>
    public int TaxYear { get; }

    public StateSupplementalCalculator(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        var root = JsonSerializer.Deserialize<Root>(json, options)
            ?? throw new InvalidOperationException("Failed to load state supplemental rate JSON data.");

        TaxYear = root.TaxYear;

        var map = new Dictionary<UsState, StateSupplementalRate>();
        foreach (var (key, value) in root.States)
        {
            if (Enum.TryParse<UsState>(key, ignoreCase: true, out var state) && value is not null)
                map[state] = value;
        }
        _rates = map;
    }

    /// <summary>
    /// Returns the configured supplemental rule for <paramref name="state"/>. States with
    /// no entry fall back to <see cref="StateSupplementalMethod.RegularMethod"/>.
    /// </summary>
    public StateSupplementalRate GetRate(UsState state)
        => _rates.TryGetValue(state, out var rate)
            ? rate
            : new StateSupplementalRate { Method = StateSupplementalMethod.RegularMethod };

    /// <summary>
    /// Computes state supplemental withholding for a payment.
    /// </summary>
    /// <param name="state">The work state.</param>
    /// <param name="supplementalWages">The supplemental payment (e.g. a bonus).</param>
    /// <param name="federalSupplementalWithholding">
    /// The federal supplemental withholding already computed for the same payment, used by
    /// states (Vermont) whose rate is a percentage of the federal amount.
    /// </param>
    public StateSupplementalResult Calculate(
        UsState state,
        decimal supplementalWages,
        decimal federalSupplementalWithholding)
    {
        var wages = Math.Max(0m, supplementalWages);
        var rate = GetRate(state);

        return rate.Method switch
        {
            StateSupplementalMethod.NoIncomeTax => BuildNoTax(state, rate),
            StateSupplementalMethod.FlatRate => BuildFlat(state, rate, wages),
            StateSupplementalMethod.FederalPercentage => BuildFederalPercentage(state, rate, federalSupplementalWithholding),
            _ => BuildRegular(state, rate),
        };
    }

    private static StateSupplementalResult BuildNoTax(UsState state, StateSupplementalRate rate)
    {
        var description = rate.Note ?? $"{state} has no state income tax, so no state withholding applies to the bonus.";
        var steps = new List<ExplanationStep>
        {
            new("No state income tax", description, 0m, $"= {Money(0m)}"),
        };
        return new StateSupplementalResult(
            0m, StateSupplementalMethod.NoIncomeTax, 0m, UsesRegularMethod: false, description,
            BuildExplanation(state, 0m, steps));
    }

    private static StateSupplementalResult BuildFlat(UsState state, StateSupplementalRate rate, decimal wages)
    {
        var withholding = RoundMoney(wages * rate.Rate);
        var description = $"{state} withholds supplemental wages at a flat {rate.Rate:P2}.";
        var steps = new List<ExplanationStep>
        {
            new("Supplemental wages", "The bonus / supplemental payment subject to state withholding.", wages, $"= {Money(wages)}"),
            new($"Apply {state} supplemental rate ({rate.Rate:P2})",
                rate.Note ?? description,
                withholding,
                $"{Money(wages)} × {rate.Rate:P2} = {Money(withholding)}"),
        };
        return new StateSupplementalResult(
            withholding, StateSupplementalMethod.FlatRate, rate.Rate, UsesRegularMethod: false, description,
            BuildExplanation(state, withholding, steps));
    }

    private static StateSupplementalResult BuildFederalPercentage(UsState state, StateSupplementalRate rate, decimal federalWithholding)
    {
        var fed = Math.Max(0m, federalWithholding);
        var withholding = RoundMoney(fed * rate.Rate);
        var description = $"{state} withholds {rate.Rate:P0} of the federal supplemental withholding.";
        var steps = new List<ExplanationStep>
        {
            new("Federal supplemental withholding", "The state rate is applied to the federal amount, not the wages.", fed, $"= {Money(fed)}"),
            new($"Apply {state} rate ({rate.Rate:P0} of federal)",
                rate.Note ?? description,
                withholding,
                $"{Money(fed)} × {rate.Rate:P0} = {Money(withholding)}"),
        };
        return new StateSupplementalResult(
            withholding, StateSupplementalMethod.FederalPercentage, rate.Rate, UsesRegularMethod: false, description,
            BuildExplanation(state, withholding, steps));
    }

    private static StateSupplementalResult BuildRegular(UsState state, StateSupplementalRate rate)
    {
        var description = rate.Note
            ?? $"{state} publishes no flat supplemental rate. Employers withhold using the regular/aggregate method — use the standard paycheck calculator with the bonus added to your wages to estimate it.";
        var steps = new List<ExplanationStep>
        {
            new("Regular method", description, null, null),
        };
        return new StateSupplementalResult(
            0m, StateSupplementalMethod.RegularMethod, 0m, UsesRegularMethod: true, description,
            BuildExplanation(state, 0m, steps));
    }

    private static LineExplanation BuildExplanation(UsState state, decimal withholding, List<ExplanationStep> steps)
        => new(
            ExplanationLineKey.StateWithholding,
            $"State Withholding ({state})",
            withholding,
            steps,
            $"{state} supplemental-wage withholding rules (2026).");

    private static string Money(decimal v) => v.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

    private static decimal RoundMoney(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    private sealed class Root
    {
        public int TaxYear { get; set; }
        public string? Source { get; set; }
        public Dictionary<string, StateSupplementalRate> States { get; set; } = new();
    }
}

/// <summary>Result of a state supplemental-withholding calculation.</summary>
/// <param name="Withholding">The state income tax withheld on the payment.</param>
/// <param name="Method">The method the state uses for supplemental wages.</param>
/// <param name="Rate">The rate applied (a flat rate, or the federal-percentage rate), or 0 when not applicable.</param>
/// <param name="UsesRegularMethod">
/// True when the state has no flat supplemental rate and the regular/aggregate method applies —
/// <paramref name="Withholding"/> is 0 and callers should surface a caveat.
/// </param>
/// <param name="Description">Human-readable summary of how the amount was derived.</param>
/// <param name="Explanation">"Show Your Work" breakdown for the state line.</param>
public sealed record StateSupplementalResult(
    decimal Withholding,
    StateSupplementalMethod Method,
    decimal Rate,
    bool UsesRegularMethod,
    string Description,
    LineExplanation Explanation);
