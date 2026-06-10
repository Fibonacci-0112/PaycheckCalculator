using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Alabama;
using PaycheckCalc.Core.Tax.Arizona;
using PaycheckCalc.Core.Tax.Arkansas;
using PaycheckCalc.Core.Tax.California;
using PaycheckCalc.Core.Tax.Colorado;
using PaycheckCalc.Core.Tax.Connecticut;
using PaycheckCalc.Core.Tax.Delaware;
using PaycheckCalc.Core.Tax.DistrictOfColumbia;
using PaycheckCalc.Core.Tax.Georgia;
using PaycheckCalc.Core.Tax.Hawaii;
using PaycheckCalc.Core.Tax.Idaho;
using PaycheckCalc.Core.Tax.Illinois;
using PaycheckCalc.Core.Tax.Indiana;
using PaycheckCalc.Core.Tax.Iowa;
using PaycheckCalc.Core.Tax.Kansas;
using PaycheckCalc.Core.Tax.Kentucky;
using PaycheckCalc.Core.Tax.Louisiana;
using PaycheckCalc.Core.Tax.Maine;
using PaycheckCalc.Core.Tax.Maryland;
using PaycheckCalc.Core.Tax.Massachusetts;
using PaycheckCalc.Core.Tax.Michigan;
using PaycheckCalc.Core.Tax.Missouri;
using PaycheckCalc.Core.Tax.Mississippi;
using PaycheckCalc.Core.Tax.Minnesota;
using PaycheckCalc.Core.Tax.Montana;
using PaycheckCalc.Core.Tax.Nebraska;
using PaycheckCalc.Core.Tax.NewJersey;
using PaycheckCalc.Core.Tax.NewMexico;
using PaycheckCalc.Core.Tax.NorthCarolina;
using PaycheckCalc.Core.Tax.NorthDakota;
using PaycheckCalc.Core.Tax.NewYork;
using PaycheckCalc.Core.Tax.Ohio;
using PaycheckCalc.Core.Tax.Oklahoma;
using PaycheckCalc.Core.Tax.Oregon;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.RhodeIsland;
using PaycheckCalc.Core.Tax.SouthCarolina;
using PaycheckCalc.Core.Tax.Utah;
using PaycheckCalc.Core.Tax.Vermont;
using PaycheckCalc.Core.Tax.Virginia;
using PaycheckCalc.Core.Tax.Washington;
using PaycheckCalc.Core.Tax.WestVirginia;
using PaycheckCalc.Core.Tax.Wisconsin;
using PaycheckCalc.Core.Tax.Wyoming;
using PaycheckCalc.Core.Tax.State;
using Xunit;

// ─── StateInputValues Tests ─────────────────────────────────────────────

public class StateInputValuesTest
{
    [Fact]
    public void GetValueOrDefault_ReturnsTypedValue()
    {
        var values = new StateInputValues { ["Count"] = 5 };
        Assert.Equal(5, values.GetValueOrDefault<int>("Count"));
    }

    [Fact]
    public void GetValueOrDefault_ReturnsFallbackWhenMissing()
    {
        var values = new StateInputValues();
        Assert.Equal(42, values.GetValueOrDefault("Missing", 42));
    }

    [Fact]
    public void GetValueOrDefault_ConvertsStringToDecimal()
    {
        var values = new StateInputValues { ["Amount"] = "123.45" };
        Assert.Equal(123.45m, values.GetValueOrDefault<decimal>("Amount"));
    }

    [Fact]
    public void GetValueOrDefault_ConvertsStringToInt()
    {
        var values = new StateInputValues { ["Count"] = "7" };
        Assert.Equal(7, values.GetValueOrDefault<int>("Count"));
    }

    [Fact]
    public void GetValueOrDefault_ReturnsFallbackOnInvalidConversion()
    {
        var values = new StateInputValues { ["Bad"] = "not-a-number" };
        Assert.Equal(0m, values.GetValueOrDefault<decimal>("Bad"));
    }

    [Fact]
    public void CaseInsensitive_KeyLookup()
    {
        var values = new StateInputValues { ["filingstatus"] = "Single" };
        Assert.Equal("Single", values.GetValueOrDefault<string>("FilingStatus"));
    }
}

// ─── StateCalculatorRegistry Tests ──────────────────────────────────────

public class StateCalculatorRegistryTest
{
    [Fact]
    public void Register_And_GetCalculator_Works()
    {
        var registry = new StateCalculatorRegistry();
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.TX));

        var calc = registry.GetCalculator(UsState.TX);

        Assert.NotNull(calc);
        Assert.Equal(UsState.TX, calc.State);
    }

    [Fact]
    public void GetCalculator_UnregisteredState_Throws()
    {
        var registry = new StateCalculatorRegistry();
        Assert.Throws<NotSupportedException>(() => registry.GetCalculator(UsState.CA));
    }

    [Fact]
    public void IsSupported_ReturnsCorrectValue()
    {
        var registry = new StateCalculatorRegistry();
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.FL));

        Assert.True(registry.IsSupported(UsState.FL));
        Assert.False(registry.IsSupported(UsState.NY));
    }

    [Fact]
    public void SupportedStates_AreSortedAlphabetically()
    {
        var registry = new StateCalculatorRegistry();
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.TX));
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.FL));
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.AK));

        var states = registry.SupportedStates;

        Assert.Equal(3, states.Count);
        Assert.Equal(UsState.AK, states[0]);
        Assert.Equal(UsState.FL, states[1]);
        Assert.Equal(UsState.TX, states[2]);
    }
}

// ─── NoIncomeTaxWithholdingAdapter Tests ────────────────────────────────

public class NoIncomeTaxWithholdingAdapterTest
{
    [Theory]
    [InlineData(UsState.AK)]
    [InlineData(UsState.FL)]
    [InlineData(UsState.TX)]
    public void ReturnsZeroWithholding(UsState state)
    {
        var calc = new NoIncomeTaxWithholdingAdapter(state);
        var context = new CommonWithholdingContext(state, 5000m, PayFrequency.Biweekly, 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Schema_IsEmpty()
    {
        var calc = new NoIncomeTaxWithholdingAdapter(UsState.TX);
        Assert.Empty(calc.GetInputSchema());
    }

    [Fact]
    public void Validate_ReturnsNoErrors()
    {
        var calc = new NoIncomeTaxWithholdingAdapter(UsState.TX);
        Assert.Empty(calc.Validate(new StateInputValues()));
    }
}

// ─── PercentageMethodWithholdingAdapter Tests ───────────────────────────

public class PercentageMethodWithholdingAdapterTest
{
    [Fact]
    public void Schema_HasFilingStatus_Allowances_AdditionalWithholding()
    {
        var calc = CreateAdapter(UsState.UT);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Equal("FilingStatus", schema[0].Key);
        Assert.Equal(StateFieldType.Picker, schema[0].FieldType);
        Assert.Equal("Allowances", schema[1].Key);
        Assert.Equal(StateFieldType.Integer, schema[1].FieldType);
        Assert.Equal("AdditionalWithholding", schema[2].Key);
        Assert.Equal(StateFieldType.Decimal, schema[2].FieldType);
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = CreateAdapter(UsState.UT);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Invalid" });
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_ValidFilingStatus_ReturnsNoErrors()
    {
        var calc = CreateAdapter(UsState.UT);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Single" });
        Assert.Empty(errors);
    }

    [Fact]
    public void Utah_FlatRate_MatchesLegacyCalculator()
    {
        // Utah is a flat 4.65% state with no standard deduction or
        // allowance in StateTaxConfigs2026, making it a clean fixture
        // for verifying the generic percentage-method adapter.
        var adapter = CreateAdapter(UsState.UT);
        var context = new CommonWithholdingContext(UsState.UT, 5000m, PayFrequency.Biweekly, 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = adapter.Calculate(context, values);

        // 5000 * 26 = 130,000 * 4.65% = 6,045 / 26 = 232.50
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(232.50m, result.Withholding);
    }

    // North Carolina now uses a dedicated calculator (NorthCarolinaWithholdingCalculator).
    // See NorthCarolinaWithholdingCalculatorTest for regression tests.
    [Fact]
    public void NorthCarolina_Married_DedicatedCalculator()
    {
        var calc = new NorthCarolinaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(UsState.NC, 6000m, PayFrequency.Monthly, 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 6000 * 12 = 72,000; std ded married = 25,500
        // taxable = 72,000 - 25,500 = 46,500; tax = 46,500 * 4.5% = 2,092.50
        // per period = 2,092.50 / 12 = 174.375 → 174.38
        Assert.Equal(174.38m, result.Withholding);
    }

    [Fact]
    public void AdditionalWithholding_IsAdded()
    {
        var adapter = CreateAdapter(UsState.UT);
        var context = new CommonWithholdingContext(UsState.UT, 5000m, PayFrequency.Biweekly, 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 50m
        };

        var result = adapter.Calculate(context, values);

        // 232.50 base + 50 extra = 282.50
        Assert.Equal(282.50m, result.Withholding);
    }

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var adapter = CreateAdapter(UsState.UT);
        var context = new CommonWithholdingContext(UsState.UT, 5000m, PayFrequency.Biweekly, 2026,
            PreTaxDeductionsReducingStateWages: 1000m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = adapter.Calculate(context, values);

        // Taxable wages = 5000 - 1000 = 4000.
        // 4000 * 26 = 104,000 * 4.65% = 4,836 / 26 = 186.00
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(186.00m, result.Withholding);
    }

    [Fact]
    public void AllConfiguredStates_ProduceResult()
    {
        foreach (var (state, config) in StateTaxConfigs2026.Configs)
        {
            var adapter = new PercentageMethodWithholdingAdapter(state, config, TestSchemas.Provider);
            var context = new CommonWithholdingContext(state, 5000m, PayFrequency.Biweekly, 2026);
            var values = new StateInputValues
            {
                ["FilingStatus"] = "Single",
                ["Allowances"] = 0,
                ["AdditionalWithholding"] = 0m
            };

            var result = adapter.Calculate(context, values);

            Assert.True(result.TaxableWages >= 0m, $"{state} should have non-negative taxable wages");
            Assert.True(result.Withholding >= 0m, $"{state} should have non-negative withholding");
        }
    }

    private static PercentageMethodWithholdingAdapter CreateAdapter(UsState state)
    {
        // The generic percentage-method adapter is no longer wired to any
        // production state (every state has a dedicated calculator, so
        // StateTaxConfigs2026.Configs is empty). Build a self-contained flat
        // 4.65% config — no standard deduction or allowance — to exercise the
        // adapter directly. This keeps the arithmetic clean for the fixtures
        // below (e.g. 5000 × 26 × 4.65% ÷ 26 = 232.50).
        var config = new PercentageMethodConfig
        {
            StandardDeductionSingle = 0m,
            StandardDeductionMarried = 0m,
            AllowanceAmount = 0m,
            AllowanceCreditAmount = 0m,
            BracketsSingle = [new TaxBracket { Floor = 0m, Rate = 0.0465m }],
            BracketsMarried = [new TaxBracket { Floor = 0m, Rate = 0.0465m }],
        };
        return new PercentageMethodWithholdingAdapter(state, config, TestSchemas.Provider);
    }
}

// ─── AlabamaWithholdingCalculator Tests ─────────────────────────────────

public class AlabamaWithholdingCalculatorTest
{
    [Fact]
    public void State_ReturnsAlabama()
    {
        var calc = new AlabamaWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.AL, calc.State);
    }

    [Fact]
    public void Schema_HasThreeFields()
    {
        var calc = new AlabamaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Equal("FilingStatus", schema[0].Key);
        Assert.Equal(StateFieldType.Picker, schema[0].FieldType);
        Assert.Equal(5, schema[0].Options!.Count); // 0, Single, MFJ, MFS, HoF
        Assert.Equal("Dependents", schema[1].Key);
        Assert.Equal(StateFieldType.Integer, schema[1].FieldType);
        Assert.Equal("AdditionalWithholding", schema[2].Key);
    }

    [Fact]
    public void Validate_ValidFilingStatus_ReturnsNoErrors()
    {
        var calc = new AlabamaWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Single" });
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new AlabamaWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Invalid" });
        Assert.Single(errors);
    }

    [Fact]
    public void SingleFiling_ProducesPositiveWithholding()
    {
        var calc = new AlabamaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(UsState.AL, 3000m, PayFrequency.Biweekly, 2026,
            FederalWithholdingPerPeriod: 200m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Dependents"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.True(result.TaxableWages > 0m);
        Assert.True(result.Withholding > 0m);
    }

    [Fact]
    public void ZeroFiling_ProducesWithholding()
    {
        var calc = new AlabamaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(UsState.AL, 3000m, PayFrequency.Biweekly, 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "0",
            ["Dependents"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.True(result.Withholding > 0m);
    }

    [Fact]
    public void MarriedFilingJointly_WithDependents_ReducesTax()
    {
        var calc = new AlabamaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(UsState.AL, 3000m, PayFrequency.Biweekly, 2026);

        var withoutDeps = calc.Calculate(context, new StateInputValues
        {
            ["FilingStatus"] = "Married Filing Jointly",
            ["Dependents"] = 0,
            ["AdditionalWithholding"] = 0m
        });

        var withDeps = calc.Calculate(context, new StateInputValues
        {
            ["FilingStatus"] = "Married Filing Jointly",
            ["Dependents"] = 3,
            ["AdditionalWithholding"] = 0m
        });

        Assert.True(withDeps.Withholding < withoutDeps.Withholding,
            "More dependents should reduce withholding");
    }

    [Fact]
    public void AdditionalWithholding_IsAdded()
    {
        var calc = new AlabamaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(UsState.AL, 3000m, PayFrequency.Biweekly, 2026);
        var baseValues = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Dependents"] = 0,
            ["AdditionalWithholding"] = 0m
        };
        var extraValues = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Dependents"] = 0,
            ["AdditionalWithholding"] = 25m
        };

        var baseResult = calc.Calculate(context, baseValues);
        var extraResult = calc.Calculate(context, extraValues);

        Assert.Equal(25m, extraResult.Withholding - baseResult.Withholding);
    }

    [Fact]
    public void AllFiveFilingStatuses_ProduceResults()
    {
        var calc = new AlabamaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(UsState.AL, 3000m, PayFrequency.Monthly, 2026);

        string[] statuses = ["0", "Single", "Married Filing Jointly", "Married Filing Separately", "Head of Family"];
        foreach (var status in statuses)
        {
            var result = calc.Calculate(context, new StateInputValues
            {
                ["FilingStatus"] = status,
                ["Dependents"] = 0,
                ["AdditionalWithholding"] = 0m
            });
            Assert.True(result.TaxableWages >= 0m, $"Alabama {status}: taxable wages should be >= 0");
            Assert.True(result.Withholding >= 0m, $"Alabama {status}: withholding should be >= 0");
        }
    }
    [Fact]
    public void FederalWithholding_ReadFromContext_ReducesStateTax()
    {
        var calc = new AlabamaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Dependents"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var contextNoFed = new CommonWithholdingContext(UsState.AL, 3000m, PayFrequency.Biweekly, 2026,
            FederalWithholdingPerPeriod: 0m);
        var contextWithFed = new CommonWithholdingContext(UsState.AL, 3000m, PayFrequency.Biweekly, 2026,
            FederalWithholdingPerPeriod: 200m);

        var resultNoFed = calc.Calculate(contextNoFed, values);
        var resultWithFed = calc.Calculate(contextWithFed, values);

        Assert.True(resultWithFed.Withholding < resultNoFed.Withholding,
            "Federal withholding passed via context should reduce Alabama state tax");
    }

    [Fact]
    public void Schema_DoesNotIncludeFederalWithholding()
    {
        var calc = new AlabamaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.DoesNotContain(schema, f => f.Key == "FederalWithholding");
    }
}

public class OklahomaWithholdingCalculatorTest
{
    private static OklahomaOw2PercentageCalculator LoadOkCalculator()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "ok_ow2_2026_percentage.json");
        var json = File.ReadAllText(dataPath);
        return new OklahomaOw2PercentageCalculator(json);
    }

    [Fact]
    public void State_ReturnsOklahoma()
    {
        var calc = new OklahomaWithholdingCalculator(LoadOkCalculator(), TestSchemas.Provider);
        Assert.Equal(UsState.OK, calc.State);
    }

    [Fact]
    public void Schema_HasThreeFields()
    {
        var calc = new OklahomaWithholdingCalculator(LoadOkCalculator(), TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Equal("FilingStatus", schema[0].Key);
        Assert.Equal("Allowances", schema[1].Key);
        Assert.Equal("AdditionalWithholding", schema[2].Key);
    }

    [Fact]
    public void SemiMonthly_Married_TwoAllowances_MatchesLegacy()
    {
        var calc = new OklahomaWithholdingCalculator(LoadOkCalculator(), TestSchemas.Provider);
        var context = new CommonWithholdingContext(UsState.OK, 1825m, PayFrequency.Semimonthly, 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["Allowances"] = 2,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(37m, result.Withholding);
    }

    [Fact]
    public void Validate_ValidStatus_ReturnsNoErrors()
    {
        var calc = new OklahomaWithholdingCalculator(LoadOkCalculator(), TestSchemas.Provider);
        Assert.Empty(calc.Validate(new StateInputValues { ["FilingStatus"] = "Married" }));
    }

    [Fact]
    public void Validate_InvalidStatus_ReturnsError()
    {
        var calc = new OklahomaWithholdingCalculator(LoadOkCalculator(), TestSchemas.Provider);
        Assert.Single(calc.Validate(new StateInputValues { ["FilingStatus"] = "Bad" }));
    }
}

// ─── PennsylvaniaWithholdingCalculator Tests ────────────────────────────

public class PennsylvaniaWithholdingCalculatorTest_New
{
    [Fact]
    public void State_ReturnsPennsylvania()
    {
        var calc = new PennsylvaniaWithholdingCalculator();
        Assert.Equal(UsState.PA, calc.State);
    }

    [Fact]
    public void Schema_HasOnlyAdditionalWithholding()
    {
        var calc = new PennsylvaniaWithholdingCalculator();
        var schema = calc.GetInputSchema();

        Assert.Single(schema);
        Assert.Equal("AdditionalWithholding", schema[0].Key);
        Assert.Equal(StateFieldType.Decimal, schema[0].FieldType);
    }

    [Fact]
    public void Validate_AlwaysReturnsNoErrors()
    {
        var calc = new PennsylvaniaWithholdingCalculator();
        Assert.Empty(calc.Validate(new StateInputValues()));
    }

    [Fact]
    public void FlatRate_MatchesLegacyCalculator()
    {
        var calc = new PennsylvaniaWithholdingCalculator();
        var context = new CommonWithholdingContext(UsState.PA, 5000m, PayFrequency.Biweekly, 2026);
        var values = new StateInputValues { ["AdditionalWithholding"] = 0m };

        var result = calc.Calculate(context, values);

        // 5000 * 0.0307 = 153.50
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(153.50m, result.Withholding);
    }

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = new PennsylvaniaWithholdingCalculator();
        var context = new CommonWithholdingContext(UsState.PA, 5000m, PayFrequency.Biweekly, 2026,
            PreTaxDeductionsReducingStateWages: 1000m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(122.80m, result.Withholding);
    }

    [Fact]
    public void AdditionalWithholding_IsAdded()
    {
        var calc = new PennsylvaniaWithholdingCalculator();
        var context = new CommonWithholdingContext(UsState.PA, 5000m, PayFrequency.Biweekly, 2026);
        var values = new StateInputValues { ["AdditionalWithholding"] = 25m };

        var result = calc.Calculate(context, values);

        Assert.Equal(178.50m, result.Withholding);
    }
}

// ─── Full Registry Integration Test ─────────────────────────────────────

public class FullRegistryIntegrationTest
{
    [Fact]
    public void AllFiftyOneStates_Registered_And_Produce_Results()
    {
        var registry = BuildFullRegistry();

        // All 50 states + DC = 51 entries
        Assert.Equal(51, registry.SupportedStates.Count);

        foreach (var state in registry.SupportedStates)
        {
            var calc = registry.GetCalculator(state);
            Assert.Equal(state, calc.State);

            // Get schema and build default values
            var schema = calc.GetInputSchema();
            var values = new StateInputValues();
            foreach (var field in schema)
            {
                if (field.DefaultValue != null)
                    values[field.Key] = field.DefaultValue;
            }

            var context = new CommonWithholdingContext(state, 5000m, PayFrequency.Biweekly, 2026);
            var result = calc.Calculate(context, values);

            Assert.True(result.TaxableWages >= 0m, $"{state}: taxable wages should be >= 0");
            Assert.True(result.Withholding >= 0m, $"{state}: withholding should be >= 0");
        }
    }

    [Fact]
    public void Alabama_Schema_DiffersFromOklahoma()
    {
        var registry = BuildFullRegistry();

        var alSchema = registry.GetCalculator(UsState.AL).GetInputSchema();
        var okSchema = registry.GetCalculator(UsState.OK).GetInputSchema();

        // Alabama has 3 fields (FilingStatus with 5 options, Dependents, AdditionalWithholding)
        // FederalWithholding is passed via CommonWithholdingContext, not shown in the UI schema
        Assert.Equal(3, alSchema.Count);
        Assert.Equal(5, alSchema[0].Options!.Count);

        // Oklahoma has 3 fields (FilingStatus with 2 options, Allowances, AdditionalWithholding)
        Assert.Equal(3, okSchema.Count);
        Assert.Equal(2, okSchema[0].Options!.Count);
    }

    [Fact]
    public void NoTaxStates_HaveEmptySchema()
    {
        var registry = BuildFullRegistry();

        UsState[] noTaxStates = [UsState.AK, UsState.FL, UsState.NV, UsState.NH, UsState.SD, UsState.TN, UsState.TX, UsState.WY];
        foreach (var state in noTaxStates)
        {
            var schema = registry.GetCalculator(state).GetInputSchema();
            Assert.Empty(schema);
        }
    }

    [Fact]
    public void Pennsylvania_HasMinimalSchema()
    {
        var registry = BuildFullRegistry();
        var schema = registry.GetCalculator(UsState.PA).GetInputSchema();

        Assert.Single(schema);
        Assert.Equal("AdditionalWithholding", schema[0].Key);
    }

    [Fact]
    public void Connecticut_ViaRegistry_CodesAF_ProduceNonZeroWithholding()
    {
        // Regression: CT must use the dedicated ConnecticutWithholdingCalculator
        // (not a generic adapter) and produce non-zero withholding for codes A–F.
        var registry = BuildFullRegistry();
        var calc = registry.GetCalculator(UsState.CT);

        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);

        foreach (var code in new[] { "Code A", "Code B", "Code C", "Code D", "Code F" })
        {
            var values = new StateInputValues { ["WithholdingCode"] = code };
            var result = calc.Calculate(context, values);

            Assert.True(result.Withholding > 0m,
                $"{code}: expected non-zero withholding for $3,000 biweekly via registry but got {result.Withholding}");
        }
    }

    private static StateCalculatorRegistry BuildFullRegistry()
    {
        var registry = new StateCalculatorRegistry();

        registry.Register(new AlabamaWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new ArizonaWithholdingCalculator());

        var arDataPath = Path.Combine(AppContext.BaseDirectory, "ar_withholding_2026.json");
        var arJson = File.ReadAllText(arDataPath);
        registry.Register(new ArkansasWithholdingCalculator(new ArkansasFormulaCalculator(arJson)));

        var caDataPath = Path.Combine(AppContext.BaseDirectory, "ca_method_b_2026.json");
        var caJson = File.ReadAllText(caDataPath);
        registry.Register(new CaliforniaWithholdingCalculator(new CaliforniaPercentageCalculator(caJson), TestSchemas.Provider));

        var okDataPath = Path.Combine(AppContext.BaseDirectory, "ok_ow2_2026_percentage.json");
        var okJson = File.ReadAllText(okDataPath);
        registry.Register(new OklahomaWithholdingCalculator(new OklahomaOw2PercentageCalculator(okJson), TestSchemas.Provider));

        registry.Register(new PennsylvaniaWithholdingCalculator());

        registry.Register(new IllinoisWithholdingCalculator());

        registry.Register(new IndianaWithholdingCalculator());

        var coDataPath = Path.Combine(AppContext.BaseDirectory, "co_dr0004_2026.json");
        var coJson = File.ReadAllText(coDataPath);
        registry.Register(new ColoradoWithholdingCalculator(coJson, TestSchemas.Provider));

        var ctDataPath = Path.Combine(AppContext.BaseDirectory, "connecticut_withholding_2026.json");
        var ctJson = File.ReadAllText(ctDataPath);
        registry.Register(new ConnecticutWithholdingCalculator(ctJson, TestSchemas.Provider));

        registry.Register(new DelawareWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new GeorgiaWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new HawaiiWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new IdahoWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new IowaWithholdingCalculator());

        registry.Register(new KansasWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new KentuckyWithholdingCalculator());
        registry.Register(new LouisianaWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new MaineWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new MarylandWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new MassachusettsWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new MichiganWithholdingCalculator());

        registry.Register(new MinnesotaWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new MississippiWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new MissouriWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new MontanaWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new NebraskaWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new NewJerseyWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new NewMexicoWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new NewYorkWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new NorthCarolinaWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new NorthDakotaWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new OhioWithholdingCalculator());

        registry.Register(new OregonWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new RhodeIslandWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new SouthCarolinaWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new UtahWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new VermontWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new VirginiaWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new WestVirginiaWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new WisconsinWithholdingCalculator(TestSchemas.Provider));

        registry.Register(new WashingtonWithholdingCalculator());

        registry.Register(new WyomingWithholdingCalculator());

        UsState[] noTaxStates = [UsState.AK, UsState.FL, UsState.NV, UsState.NH, UsState.SD, UsState.TN, UsState.TX];
        foreach (var state in noTaxStates)
            registry.Register(new NoIncomeTaxWithholdingAdapter(state));

        foreach (var (state, config) in StateTaxConfigs2026.Configs)
            registry.Register(new PercentageMethodWithholdingAdapter(state, config, TestSchemas.Provider));

        return registry;
    }
}
