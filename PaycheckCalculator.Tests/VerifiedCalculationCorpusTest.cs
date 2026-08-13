using Microsoft.Extensions.DependencyInjection;
using PaycheckCalculator.Core.DependencyInjection;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Golden vectors and cross-cutting invariants for the production calculation pipeline.
/// Expected federal values come from IRS Publication 15-T (2026), Worksheet 1A and the
/// annual percentage-method tables.
/// </summary>
public sealed class VerifiedCalculationCorpusTest
{
    [Theory]
    [InlineData(FederalFilingStatus.SingleOrMarriedSeparately, 16_100, 0)]
    [InlineData(FederalFilingStatus.SingleOrMarriedSeparately, 28_500, 1_240)]
    [InlineData(FederalFilingStatus.SingleOrMarriedSeparately, 78_000, 8_330)]
    [InlineData(FederalFilingStatus.MarriedFilingJointly, 78_000, 5_000)]
    public void Pub15T2026_Worksheet1A_AnnualGoldenVectors(
        FederalFilingStatus filingStatus,
        decimal annualWages,
        decimal expectedWithholding)
    {
        // IRS Publication 15-T (2026), Worksheet 1A and the STANDARD annual
        // percentage-method tables:
        // https://www.irs.gov/pub/irs-pdf/p15t.pdf
        var calculator = CreateFederalCalculator();
        var w4 = new FederalW4Input { FilingStatus = filingStatus };

        var actual = calculator.CalculateWithholding(annualWages, PayFrequency.Annual, w4);

        Assert.Equal(expectedWithholding, actual);
    }

    [Fact]
    public void Pub15T2026_Worksheet1A_BiweeklyGoldenVector()
    {
        // Single, $3,000 biweekly:
        // $3,000 × 26 = $78,000; Worksheet 1A line 1g subtracts $8,600;
        // $69,400 falls in the $57,900 bracket:
        // ($5,800 + ($69,400 - $57,900) × 22%) / 26 = $320.38.
        var calculator = CreateFederalCalculator();
        var w4 = new FederalW4Input
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
        };

        var actual = calculator.CalculateWithholding(3_000m, PayFrequency.Biweekly, w4);

        Assert.Equal(320.38m, actual);
    }

    [Fact]
    public void Pub15T2026_Worksheet1A_W4AdjustmentsGoldenVector()
    {
        // The same $3,000 biweekly vector has tentative annual withholding of
        // $8,330. A $4,400 annual Step 3 credit and $25 Step 4(c) amount produce:
        // (($8,330 - $4,400) / 26) + $25 = $176.1538... -> $176.15.
        var calculator = CreateFederalCalculator();
        var w4 = new FederalW4Input
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            Step3TaxCredits = 4_400m,
            Step4cExtraWithholding = 25m
        };

        var actual = calculator.CalculateWithholding(3_000m, PayFrequency.Biweekly, w4);

        Assert.Equal(176.15m, actual);
    }

    [Fact]
    public void ProductionRegistry_AllJurisdictionsHaveSchemaAndSmokeResult()
    {
        using var provider = CreateServices();
        var registry = provider.GetRequiredService<StateCalculatorRegistry>();
        var expectedStates = Enum.GetValues<UsState>().OrderBy(state => state.ToString()).ToArray();

        Assert.Equal(expectedStates, registry.SupportedStates);

        foreach (var state in expectedStates)
        {
            var calculator = registry.GetCalculator(state);
            var schema = calculator.GetInputSchema();
            var values = BuildDefaultValues(schema);

            Assert.Equal(state, calculator.State);
            Assert.Equal(
                schema.Count,
                schema.Select(field => field.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());

            var result = calculator.Calculate(
                new CommonWithholdingContext(
                    state,
                    GrossWages: 2_500m,
                    PayPeriod: PayFrequency.Biweekly,
                    Year: 2026,
                    FederalWithholdingPerPeriod: 200m),
                values);

            Assert.True(result.TaxableWages >= 0m, $"{state} taxable wages were negative.");
            Assert.True(result.Withholding >= 0m, $"{state} withholding was negative.");
            Assert.True(result.DisabilityInsurance >= 0m, $"{state} payroll assessment was negative.");
        }
    }

    [Fact]
    public void ProductionPaycheckCorpus_TiesOutAndIsDeterministicForEveryJurisdiction()
    {
        using var provider = CreateServices();
        var calculator = provider.GetRequiredService<PayCalculator>();
        var registry = provider.GetRequiredService<StateCalculatorRegistry>();

        foreach (var state in Enum.GetValues<UsState>())
        {
            var input = new PaycheckInput
            {
                State = state,
                Frequency = PayFrequency.Biweekly,
                HourlyRate = 37.13m,
                RegularHours = 80m,
                OvertimeHours = 3.25m,
                OvertimeMultiplier = 1.5m,
                YtdSocialSecurityWages = 184_400m,
                YtdMedicareWages = 199_900m,
                FederalW4 = new FederalW4Input
                {
                    FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
                    Step4cExtraWithholding = 11.17m
                },
                StateInputValues = BuildDefaultValues(registry.GetCalculator(state).GetInputSchema()),
                Deductions =
                [
                    new Deduction
                    {
                        Name = "Traditional 401(k)",
                        Type = DeductionType.PreTax,
                        Amount = 5m,
                        AmountType = DeductionAmountType.Percentage,
                        ReducesFederalTaxableWages = true,
                        ReducesStateTaxableWages = true,
                        ReducesFicaWages = false
                    },
                    new Deduction
                    {
                        Name = "Section 125 medical",
                        Type = DeductionType.PreTax,
                        Amount = 83.17m,
                        ReducesFederalTaxableWages = true,
                        ReducesStateTaxableWages = true,
                        ReducesFicaWages = true
                    },
                    new Deduction
                    {
                        Name = "After-tax benefit",
                        Type = DeductionType.PostTax,
                        Amount = 27.43m
                    }
                ]
            };

            var first = calculator.Calculate(input);
            var second = calculator.Calculate(input);
            var expectedNet = first.GrossPay
                - first.PreTaxDeductions
                - first.PostTaxDeductions
                - first.TotalTaxes;

            Assert.Equal(expectedNet, first.NetPay);
            AssertNonNegativeTaxes(first, state);
            AssertSameResult(first, second);
        }
    }

    [Theory]
    [InlineData(DeductionType.PreTax, true, true, false, 900, 900, 1_000, 100, 0)]
    [InlineData(DeductionType.PreTax, true, true, true, 900, 900, 900, 100, 0)]
    [InlineData(DeductionType.PreTax, true, false, false, 900, 1_000, 1_000, 100, 0)]
    [InlineData(DeductionType.PreTax, false, false, false, 1_000, 1_000, 1_000, 100, 0)]
    [InlineData(DeductionType.PostTax, true, true, true, 1_000, 1_000, 1_000, 0, 100)]
    public void DeductionTaxabilityMatrix_AppliesOnlySelectedWageReductions(
        DeductionType type,
        bool reducesFederal,
        bool reducesState,
        bool reducesFica,
        decimal expectedFederalWages,
        decimal expectedStateWages,
        decimal expectedFicaWages,
        decimal expectedPreTax,
        decimal expectedPostTax)
    {
        using var provider = CreateServices();
        var calculator = provider.GetRequiredService<PayCalculator>();
        var input = new PaycheckInput
        {
            State = UsState.PA,
            Frequency = PayFrequency.Monthly,
            PayType = PayType.Salary,
            SalaryBasis = SalaryBasis.PerPeriod,
            SalaryAmount = 1_000m,
            Deductions =
            [
                new Deduction
                {
                    Name = "Corpus deduction",
                    Type = type,
                    Amount = 100m,
                    ReducesFederalTaxableWages = reducesFederal,
                    ReducesStateTaxableWages = reducesState,
                    ReducesFicaWages = reducesFica
                }
            ]
        };

        var result = calculator.Calculate(input);

        Assert.Equal(expectedFederalWages, result.FederalTaxableIncome);
        Assert.Equal(expectedStateWages, result.StateTaxableWages);
        Assert.Equal(expectedFicaWages, result.FicaTaxableWages);
        Assert.Equal(expectedPreTax, result.PreTaxDeductions);
        Assert.Equal(expectedPostTax, result.PostTaxDeductions);
    }

    private static Irs15TPercentageCalculator CreateFederalCalculator() =>
        new(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "us_irs_15t_2026_percentage_automated.json")));

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddPaycheckCalculatorCore(new OutputTaxDataReader());
        return services.BuildServiceProvider();
    }

    private static StateInputValues BuildDefaultValues(IReadOnlyList<StateFieldDefinition> schema)
    {
        var values = new StateInputValues();
        foreach (var field in schema)
        {
            values[field.Key] = field.DefaultValue ?? field.FieldType switch
            {
                StateFieldType.Integer => 0,
                StateFieldType.Decimal => 0m,
                StateFieldType.Toggle => false,
                StateFieldType.Picker => field.Options?.FirstOrDefault() ?? "",
                _ => ""
            };
        }

        return values;
    }

    private static void AssertNonNegativeTaxes(PaycheckResult result, UsState state)
    {
        Assert.True(result.FederalWithholding >= 0m, $"{state} federal withholding was negative.");
        Assert.True(result.StateWithholding >= 0m, $"{state} state withholding was negative.");
        Assert.True(result.StateDisabilityInsurance >= 0m, $"{state} payroll assessment was negative.");
        Assert.True(result.SocialSecurityWithholding >= 0m, $"{state} Social Security was negative.");
        Assert.True(result.MedicareWithholding >= 0m, $"{state} Medicare was negative.");
        Assert.True(result.AdditionalMedicareWithholding >= 0m, $"{state} Additional Medicare was negative.");
    }

    private static void AssertSameResult(PaycheckResult expected, PaycheckResult actual)
    {
        Assert.Equal(expected.GrossPay, actual.GrossPay);
        Assert.Equal(expected.PreTaxDeductions, actual.PreTaxDeductions);
        Assert.Equal(expected.PostTaxDeductions, actual.PostTaxDeductions);
        Assert.Equal(expected.FederalTaxableIncome, actual.FederalTaxableIncome);
        Assert.Equal(expected.FicaTaxableWages, actual.FicaTaxableWages);
        Assert.Equal(expected.StateTaxableWages, actual.StateTaxableWages);
        Assert.Equal(expected.FederalWithholding, actual.FederalWithholding);
        Assert.Equal(expected.StateWithholding, actual.StateWithholding);
        Assert.Equal(expected.StateDisabilityInsurance, actual.StateDisabilityInsurance);
        Assert.Equal(expected.SocialSecurityWithholding, actual.SocialSecurityWithholding);
        Assert.Equal(expected.MedicareWithholding, actual.MedicareWithholding);
        Assert.Equal(expected.AdditionalMedicareWithholding, actual.AdditionalMedicareWithholding);
        Assert.Equal(expected.NetPay, actual.NetPay);
    }

    private sealed class OutputTaxDataReader : ITaxDataReader
    {
        public string ReadAllText(string logicalName)
        {
            var relative = logicalName.Replace('/', Path.DirectorySeparatorChar);
            var path = Path.Combine(AppContext.BaseDirectory, relative);
            if (!File.Exists(path) &&
                relative.StartsWith($"schemas{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                path = Path.Combine(AppContext.BaseDirectory, "Schemas", relative[8..]);
            }

            return File.ReadAllText(path);
        }
    }
}
