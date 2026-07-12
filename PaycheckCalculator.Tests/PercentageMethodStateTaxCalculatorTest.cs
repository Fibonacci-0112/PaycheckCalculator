using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class PercentageMethodWithholdingAdapterExtendedTest
{
    // ── Flat-rate state tests ────────────────────────────────────────

    // Utah now uses a dedicated calculator (UtahWithholdingCalculator).
    // See UtahWithholdingCalculatorTest for regression tests.

    [Fact]
    public void Utah_UsesDedicatedCalculator_NotInGenericConfigs()
    {
        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.UT),
            "UT should not be in generic StateTaxConfigs2026 — it uses UtahWithholdingCalculator.");
    }

    // ── Graduated-bracket state tests ────────────────────────────────

    // Wisconsin now uses a dedicated calculator (WisconsinWithholdingCalculator).
    // See WisconsinWithholdingCalculatorTest for regression tests.

    [Fact]
    public void Wisconsin_UsesDedicatedCalculator_NotInGenericConfigs()
    {
        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.WI),
            "WI should not be in generic StateTaxConfigs2026 — it uses WisconsinWithholdingCalculator.");
    }

    // Illinois uses a dedicated calculator (IllinoisWithholdingCalculator)

    // North Carolina now uses a dedicated calculator (NorthCarolinaWithholdingCalculator).
    // See NorthCarolinaWithholdingCalculatorTest for regression tests.

    // ── Graduated-bracket state tests ────────────────────────────────

    [Fact]
    public void Virginia_GraduatedBrackets_Single()
    {
        // Virginia now uses a dedicated calculator (VirginiaWithholdingCalculator).
        // See VirginiaWithholdingCalculatorTest for regression tests.
        // This test verifies VA is no longer in the generic percentage method configs.
        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.VA),
            "VA should not be in generic StateTaxConfigs2026 — it uses VirginiaWithholdingCalculator.");
    }

    [Fact]
    public void California_GraduatedBrackets_Single()
    {
        // California now uses a dedicated Method B calculator (CaliforniaPercentageCalculator).
        // See CaliforniaPercentageCalculatorTest for Method B tests.
        // This test verifies CA is no longer in the generic percentage method configs.
        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.CA),
            "CA should not be in generic StateTaxConfigs2026 — it uses CaliforniaPercentageCalculator.");
    }

    // New York now uses a dedicated calculator (NewYorkWithholdingCalculator).
    // See NewYorkWithholdingCalculatorTest for regression tests.

    // Ohio now uses a dedicated calculator (OhioWithholdingCalculator).
    // See OhioWithholdingCalculatorTest for regression tests.

    [Fact]
    public void Ohio_UsesDedicatedCalculator_NotInGenericConfigs()
    {
        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.OH),
            "OH should not be in generic StateTaxConfigs2026 — it uses OhioWithholdingCalculator.");
    }

    // ── Allowance credit tests ───────────────────────────────────────
    // Delaware now uses a dedicated calculator (DelawareWithholdingCalculator).
    // See DelawareWithholdingCalculatorTest for allowance credit coverage.

    [Fact]
    public void Nebraska_UsesDedicatedCalculator_NotInGenericConfigs()
    {
        // Nebraska now uses a dedicated calculator (NebraskaWithholdingCalculator).
        // See NebraskaWithholdingCalculatorTest for allowance credit and bracket tests.
        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.NE),
            "NE should not be in generic StateTaxConfigs2026 — it uses NebraskaWithholdingCalculator.");
    }

    // ── Additional withholding test ──────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAdded()
    {
        // Verify the generic adapter correctly passes extra withholding through.
        // Uses a synthetic flat-rate config (5% single bracket) to isolate the behavior.
        var calc = CreateSyntheticAdapter(UsState.WI);

        var context = new CommonWithholdingContext(
            UsState.WI,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 50m
        };

        var result = calc.Calculate(context, values);

        // annual = 5000 × 26 = 130,000; std ded = 10,000 → taxable = 120,000
        // tax = 120,000 × 5% = 6,000; per period = 6,000 / 26 = 230.769... → 230.77
        // + 50 extra = 280.77
        Assert.Equal(280.77m, result.Withholding);
    }

    // ── Pre-tax deductions test ──────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = CreateSyntheticAdapter(UsState.WI);

        var context = new CommonWithholdingContext(
            UsState.WI,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 1000m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // taxable wages = 5000 - 1000 = 4000
        // annual = 4000 × 26 = 104,000; std ded = 10,000 → taxable = 94,000
        // tax = 94,000 × 5% = 4,700; per period = 4,700 / 26 = 180.769... → 180.77
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(180.77m, result.Withholding);
    }

    // ── Zero wages edge case ─────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = CreateSyntheticAdapter(UsState.WI);

        var context = new CommonWithholdingContext(
            UsState.WI,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── State property test ──────────────────────────────────────────

    [Fact]
    public void State_ReturnsCorrectState()
    {
        var calc = CreateSyntheticAdapter(UsState.WI);
        Assert.Equal(UsState.WI, calc.State);
    }

    // ── All configured states produce a result ───────────────────────

    [Fact]
    public void AllConfiguredStates_ProduceResult()
    {
        foreach (var (state, config) in StateTaxConfigs2026.Configs)
        {
            var calc = new PercentageMethodWithholdingAdapter(state, config, TestSchemas.Provider);
            var context = new CommonWithholdingContext(
                state,
                GrossWages: 5000m,
                PayPeriod: PayFrequency.Biweekly,
                Year: 2026);
            var values = new StateInputValues
            {
                ["FilingStatus"] = "Single",
                ["Allowances"] = 0,
                ["AdditionalWithholding"] = 0m
            };

            var result = calc.Calculate(context, values);

            Assert.True(result.TaxableWages >= 0m, $"{state} should have non-negative taxable wages");
            Assert.True(result.Withholding >= 0m, $"{state} should have non-negative withholding");
        }
    }

    [Fact]
    public void AllExpectedStates_AreConfigured()
    {
        // Arizona is intentionally absent: it uses the dedicated
        // ArizonaWithholdingCalculator (Form A-4 percentage election)
        // rather than the generic annualized percentage method.
        //
        // District of Columbia is also absent: it uses the dedicated
        // DistrictOfColumbiaWithholdingCalculator (D-4 annualized
        // percentage method).
        //
        // Hawaii is also absent: it uses the dedicated
        // HawaiiWithholdingCalculator (Booklet A percentage method with
        // HW-4 allowances).
        //
        // Idaho is also absent: it uses the dedicated
        // IdahoWithholdingCalculator (flat 5.3% with ID W-4 filing-status
        // standard deduction and per-allowance amount).
        //
        // Indiana is also absent: it uses the dedicated
        // IndianaWithholdingCalculator (flat 3.05% with WH-4 personal
        // and additional dependent exemptions).
        //
        // Iowa is also absent: it uses the dedicated
        // IowaWithholdingCalculator (flat 3.65% with optional extra
        // withholding per IA W-4 Line 6).
        //
        // Kansas is also absent: it uses the dedicated
        // KansasWithholdingCalculator (K-4 filing status, $3,605/$8,240
        // standard deduction, $2,250 per K-4 allowance, two graduated
        // brackets 5.20%/5.58%).
        //
        // Kentucky is also absent: it uses the dedicated
        // KentuckyWithholdingCalculator (flat 4.0% with $3,160 standard
        // deduction and $10 K-4 allowance credit).
        //
        // Louisiana is also absent: it uses the dedicated
        // LouisianaWithholdingCalculator (L-4 filing statuses, $4,500/$9,000
        // personal exemption, $1,000 per-dependent deduction, and three
        // graduated brackets 1.85%/3.50%/4.25% per R-1306).
        //
        // Maine is also absent: it uses the dedicated
        // MaineWithholdingCalculator (W-4ME filing status, $15,300/$30,600
        // standard deduction, $5,300 allowance, three graduated brackets).
        //
        // Maryland is also absent: it uses the dedicated
        // MarylandWithholdingCalculator (MW507 filing statuses, variable
        // standard deduction 15% of wages with min/max, $3,200 per exemption,
        // ten graduated brackets 2%–6.5%).
        //
        // Massachusetts is also absent: it uses the dedicated
        // MassachusettsWithholdingCalculator (M-4 filing statuses, personal/
        // dependent/blind/age exemptions, flat 5% with 4% surtax above $1M).
        //
        // Michigan is also absent: it uses the dedicated
        // MichiganWithholdingCalculator (flat 4.25% with MI-W4 exemptions).
        //
        // Minnesota is also absent: it uses the dedicated
        // MinnesotaWithholdingCalculator (W-4MN filing statuses Single/Married/
        // Head of Household, $15,300/$30,600/$23,000 standard deduction,
        // $5,300 per W-4MN allowance, four graduated brackets 5.35%/6.80%/7.85%/9.85%).
        //
        // Mississippi is also absent: it uses the dedicated
        // MississippiWithholdingCalculator (89-350 filing statuses Single/Married/HoH,
        // filing-status standard deduction and personal exemption, two brackets
        // 0%/$0–$10,000 and 4% over $10,000).
        //
        // Missouri is also absent: it uses the dedicated
        // MissouriWithholdingCalculator (MO W-4 filing statuses Single/Married/HoH,
        // $15,750/$31,500/$23,625 standard deduction, $2,100 allowance, eight brackets 0%–4.7%).
        //
        // Montana is also absent: it uses the dedicated
        // MontanaWithholdingCalculator (MW-4 filing statuses, variable 20% standard
        // deduction with min/max, $3,040 per exemption, two brackets 4.7%/5.9%).
        //
        // Nebraska is also absent: it uses the dedicated
        // NebraskaWithholdingCalculator (W-4N filing statuses Single/Married/HoH,
        // $8,600/$17,200/$12,900 standard deduction, $171 per-allowance credit,
        // four brackets 2.46%/3.51%/5.01%/5.2%).
        //
        // New Jersey is also absent: it uses the dedicated
        // NewJerseyWithholdingCalculator (NJ-W4 statuses A–E, $1,000 allowance
        // deduction, Table A/B brackets).
        //
        // North Carolina is also absent: it uses the dedicated
        // NorthCarolinaWithholdingCalculator (NC-4 filing statuses Single/Married/
        // Head of Household, $12,750/$25,500/$19,125 standard deduction,
        // $2,500 per NC-4 allowance, flat 4.5% per NC DOR Publication NC-30 (2026)).
        //
        // New York is also absent: it uses the dedicated
        // NewYorkWithholdingCalculator (IT-2104 filing statuses Single/Married/
        // Head of Household, $8,000/$16,050/$11,000 standard deduction,
        // $1,000 per IT-2104 allowance, ten graduated brackets 4%–10.9%
        // per NYS Publication NYS-50-T-NYS (2026)).
        //
        // North Dakota is also absent: it uses the dedicated
        // NorthDakotaWithholdingCalculator (federal W-4 filing statuses Single/Married/
        // Head of Household, $15,750/$31,500/$23,625 standard deduction (mirrors federal),
        // three graduated brackets 1.10%/2.04%/2.64% per the ND Office of State Tax
        // Commissioner 2026 Employer's Withholding Guide).
        //
        // Ohio is also absent: it uses the dedicated OhioWithholdingCalculator
        // (IT-4 exemption allowance $650 annualized per exemption, no filing status,
        // two brackets 0% on $0–$26,050 and 2.75% over $26,050 per the Ohio Department
        // of Taxation 2026 Employer Withholding Tax – Optional Computer Formula).
        //
        // Oregon is also absent: it uses the dedicated OregonWithholdingCalculator
        // (OR-W-4 filing statuses Single/Married/Head of Household, $2,835/$5,670/$2,835
        // standard deduction where HoH uses Single deduction, $219 per OR-W-4 allowance
        // credit, and four graduated brackets 4.75%/6.75%/8.75%/9.9% where HoH uses
        // Married bracket thresholds per Oregon DOR Publication 150-206-436 (2026)).
        //
        // Rhode Island is also absent: it uses the dedicated RhodeIslandWithholdingCalculator
        // (RI W-4 filing statuses Single/Married/Head of Household, $10,550 standard
        // deduction same for all filing statuses, $4,700 per RI W-4 exemption, and three
        // graduated brackets 3.75%/4.75%/5.99% per the RI Division of Taxation 2026 Pub. T-174).
        //
        // South Carolina is also absent: it uses the dedicated SouthCarolinaWithholdingCalculator
        // (SC W-4 filing statuses Single/Married/Head of Household, variable standard deduction
        // 10% of annualized wages max $7,500 when allowances ≥ 1, $5,000 per SC W-4 allowance,
        // and three graduated brackets 0%/3%/6% at $0/$3,640/$18,230 per SCDOR WH-1603F (2026)).
        //
        // Utah is also absent: it uses the dedicated UtahWithholdingCalculator
        // (federal W-4 statuses Single/Married, flat 4.5% with phase-out allowance credit).
        //
        // Vermont is also absent: it uses the dedicated VermontWithholdingCalculator
        // (W-4VT filing statuses Single/Married/HoH, $5,400/allowance, four brackets
        // 3.35%/6.60%/7.60%/8.75% per Vermont Department of Taxes BP-55 (2026)).
        //
        // West Virginia is also absent: it uses the dedicated WestVirginiaWithholdingCalculator
        // (IT-104 filing statuses Single/Married, no state standard deduction, $2,000/exemption,
        // five brackets 3%/4%/4.5%/6%/6.5% per WV State Tax Dept. Form IT-104 (2026)).
        //
        // Wisconsin is also absent: it uses the dedicated WisconsinWithholdingCalculator
        // (WT-4 filing statuses Single/Married/Head of Household, $12,760/$23,170/$16,840
        // standard deduction, $700 per WT-4 allowance, four brackets 3.54%/4.65%/5.30%/7.65%
        // per WI DOR Publication W-166 (2026)).
        //
        // All states now use dedicated calculators; the generic percentage-method config
        // dictionary is intentionally empty.
        Assert.Empty(StateTaxConfigs2026.Configs);

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.AZ),
            "AZ should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.DC),
            "DC should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.HI),
            "HI should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.IA),
            "IA should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.ID),
            "ID should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.IN),
            "IN should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.KS),
            "KS should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.KY),
            "KY should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.LA),
            "LA should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.MD),
            "MD should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.ME),
            "ME should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.MI),
            "MI should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.MA),
            "MA should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.MI),
            "MI should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.MN),
            "MN should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.MO),
            "MO should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.MS),
            "MS should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.MT),
            "MT should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.NE),
            "NE should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.NJ),
            "NJ should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.NM),
            "NM should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.NY),
            "NY should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.NC),
            "NC should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.ND),
            "ND should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.OR),
            "OR should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.RI),
            "RI should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.SC),
            "SC should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.UT),
            "UT should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.VA),
            "VA should not be in StateTaxConfigs2026 — it uses VirginiaWithholdingCalculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.VT),
            "VT should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.WI),
            "WI should not be in StateTaxConfigs2026 — it uses WisconsinWithholdingCalculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.WV),
            "WV should not be in StateTaxConfigs2026 — it uses WestVirginiaWithholdingCalculator.");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds a synthetic flat-rate adapter for the given state.
    /// Used to test generic adapter behavior (additional withholding, pre-tax
    /// deductions, zero wages) without relying on StateTaxConfigs2026 entries
    /// that have been superseded by dedicated calculators.
    /// </summary>
    private static PercentageMethodWithholdingAdapter CreateSyntheticAdapter(UsState state)
    {
        var config = new PercentageMethodConfig
        {
            StandardDeductionSingle = 10_000m,
            StandardDeductionMarried = 20_000m,
            BracketsSingle = [new TaxBracket { Floor = 0m, Ceiling = null, Rate = 0.05m }],
            BracketsMarried = [new TaxBracket { Floor = 0m, Ceiling = null, Rate = 0.05m }]
        };
        return new PercentageMethodWithholdingAdapter(state, config, TestSchemas.Provider);
    }
}
