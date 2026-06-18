using System.Text.Json;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Sources;
using PaycheckCalc.Core.Tax.State;
using PaycheckCalc.Core.Tax.TaxYears;
using PaycheckCalc.Shared.Json;
using PaycheckCalc.Shared.Snapshots;
using Xunit;

namespace PaycheckCalc.Tests;

public sealed class TaxYearInfrastructureTest
{
    [Fact]
    public void NewPaycheckInput_DefaultsToBundledTaxYear()
    {
        var input = new PaycheckInput();

        Assert.Equal(FixedTaxYearProvider.BundledTaxYear, input.TaxYear);
    }

    [Fact]
    public void LegacySavedPaycheckJson_MigratesMissingTaxYearToBundledYear()
    {
        const string json = """
        {
          "name":"Legacy",
          "updatedAtUtc":"2026-01-01T00:00:00+00:00",
          "input":{"frequency":"Biweekly","state":"TX","hourlyRate":25,"regularHours":80},
          "result":{"grossPay":2000,"netPay":1700,"totalTaxes":300}
        }
        """;

        var dto = JsonSerializer.Deserialize<SavedPaycheckDto>(json, PaycheckJson.Options)!;

        Assert.Equal(FixedTaxYearProvider.BundledTaxYear, dto.TaxYear);
        Assert.Equal(FixedTaxYearProvider.BundledTaxYear, dto.Input.TaxYear);
        Assert.Equal(FixedTaxYearProvider.BundledTaxYear, dto.Result.TaxYear);
    }

    [Theory]
    [InlineData(2026)]
    [InlineData(2027)]
    public void PayCalculator_CarriesInputTaxYearIntoResultAndStateContext(int taxYear)
    {
        var stateCalculator = new CapturingStateCalculator();
        var registry = new StateCalculatorRegistry();
        registry.Register(stateCalculator);
        var calculator = new PayCalculator(registry, new FicaCalculator(), new Irs15TPercentageCalculator(FedJson()));

        var result = calculator.Calculate(new PaycheckInput
        {
            TaxYear = taxYear,
            Frequency = PayFrequency.Biweekly,
            State = UsState.TX,
            HourlyRate = 25m,
            RegularHours = 80m
        });

        Assert.Equal(taxYear, result.TaxYear);
        Assert.Equal(taxYear, stateCalculator.SeenYear);
    }

    [Fact]
    public void SourceMetadata_IsFilteredByTaxYear()
    {
        var provider = new StaticTaxSourceMetadataProvider();

        Assert.NotEmpty(provider.GetSources(2026));
        Assert.Empty(provider.GetSources(2027));
    }

    private static string FedJson() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "us_irs_15t_2026_percentage_automated.json"));

    private sealed class CapturingStateCalculator : IStateWithholdingCalculator
    {
        public UsState State => UsState.TX;
        public int SeenYear { get; private set; }
        public IReadOnlyList<string> Validate(StateInputValues values) => Array.Empty<string>();
        public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues inputs)
        {
            SeenYear = context.Year;
            return new StateWithholdingResult { Description = "captured" };
        }
    }
}
