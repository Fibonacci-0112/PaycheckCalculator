using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Fica;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.State;
using PaycheckCalculator.Core.Tax.Sources;

namespace PaycheckCalculator.Core.Pay;

public sealed class PayCalculator
{
    private readonly StateCalculatorRegistry _stateRegistry;
    private readonly FicaCalculator _fica;
    private readonly Irs15TPercentageCalculator _fed;
    private readonly TaxSourceCatalog? _sourceCatalog;

    public PayCalculator(
        StateCalculatorRegistry stateRegistry,
        FicaCalculator fica,
        Irs15TPercentageCalculator fed,
        TaxSourceCatalog? sourceCatalog = null)
    {
        _stateRegistry = stateRegistry;
        _fica = fica;
        _fed = fed;
        _sourceCatalog = sourceCatalog;
    }

    public PaycheckResult Calculate(PaycheckInput input)
    {
        if (!TaxYearSupport.IsSupported(input.TaxYear))
            throw new NotSupportedException(
                $"Tax year {input.TaxYear} is not supported. Only {TaxYearSupport.Default} tax data is currently loaded.");

        var payPeriods = PayPeriods.PerYear(input.Frequency);
        var gross = input.PayType == PayType.Salary
            ? (input.SalaryBasis == SalaryBasis.PerYear
                ? input.SalaryAmount / payPeriods
                : input.SalaryAmount)
            : (input.RegularHours * input.HourlyRate)
                + (input.OvertimeHours * input.HourlyRate * input.OvertimeMultiplier);

        var preTax = input.Deductions.Where(d => d.Type == DeductionType.PreTax).Sum(d => d.EffectiveAmount(gross));
        var postTax = input.Deductions.Where(d => d.Type == DeductionType.PostTax).Sum(d => d.EffectiveAmount(gross));

        var preTaxState = input.Deductions.Where(d => d.Type == DeductionType.PreTax && d.ReducesStateTaxableWages).Sum(d => d.EffectiveAmount(gross));

        // 401(k)/403(b)/457 deductions reduce federal/state taxable income but NOT FICA wages.
        // Only Section 125 deductions (ReducesFicaWages = true) are excluded from FICA wages.
        var ficaPreTax = input.Deductions.Where(d => d.Type == DeductionType.PreTax && d.ReducesFicaWages).Sum(d => d.EffectiveAmount(gross));
        var ficaWages = Math.Max(0m, gross - ficaPreTax);
        var ficaDetail = _fica.CalculateWithExplanation(ficaWages, input.YtdSocialSecurityWages, input.YtdMedicareWages);
        var ss = ficaDetail.SocialSecurity;
        var medicare = ficaDetail.Medicare;
        var addl = ficaDetail.AdditionalMedicare;

        // Roth 401(k)/403(b) and similar after-tax retirement deductions do NOT
        // reduce federal taxable income. Only deductions with
        // ReducesFederalTaxableWages = true reduce it.
        var fedPreTax = input.Deductions.Where(d => d.Type == DeductionType.PreTax && d.ReducesFederalTaxableWages).Sum(d => d.EffectiveAmount(gross));
        var fedTaxable = Math.Max(0m, gross - fedPreTax);
        var fedDetail = _fed.CalculateWithExplanation(fedTaxable, input.Frequency, input.FederalW4);
        var federal = fedDetail.Withholding;

        var calc = _stateRegistry.GetCalculator(input.State);
        var context = new CommonWithholdingContext(
            input.State,
            gross,
            input.Frequency,
            Year: input.TaxYear,
            PreTaxDeductionsReducingStateWages: preTaxState,
            FederalWithholdingPerPeriod: RoundMoney(federal),
            YtdStateWages: input.YtdStateWages);
        var stateValues = input.StateInputValues ?? new StateInputValues();
        var stateResult = calc.Calculate(context, stateValues);

        // The displayed state lines are the source of truth for the state portion:
        // a jurisdiction may levy several (New Jersey withholds SDI and FLI; Maryland
        // withholds state and county income tax), and net must be derived from the
        // same already-rounded amounts the user sees.
        var stateTaxLines = StateTaxLineResolver.Resolve(stateResult);
        var stateIncomeTotal = SumLines(stateTaxLines, assessments: false);
        var stateAssessmentTotal = SumLines(stateTaxLines, assessments: true);

        // Derive net from the same cent-rounded components returned to callers so
        // every displayed paycheck balances exactly.
        var net = RoundMoney(gross) - RoundMoney(preTax) - RoundMoney(postTax)
                - stateIncomeTotal - stateAssessmentTotal
                - RoundMoney(ss) - RoundMoney(medicare) - RoundMoney(addl) - RoundMoney(federal);

        var explanation = BuildExplanation(
            grossPay: RoundMoney(gross),
            payType: input.PayType,
            salaryAmount: RoundMoney(input.SalaryAmount),
            salaryBasis: input.SalaryBasis,
            payPeriods: payPeriods,
            regularHours: input.RegularHours,
            hourlyRate: input.HourlyRate,
            overtimeHours: input.OvertimeHours,
            overtimeMultiplier: input.OvertimeMultiplier,
            preTax: RoundMoney(preTax),
            postTax: RoundMoney(postTax),
            federalTaxableIncome: RoundMoney(fedTaxable),
            federalPreTaxReducing: RoundMoney(fedPreTax),
            ficaTaxableWages: RoundMoney(ficaWages),
            ficaPreTaxReducing: RoundMoney(ficaPreTax),
            federalWithholding: RoundMoney(federal),
            federalExplanation: fedDetail.Explanation,
            ficaDetail: ficaDetail,
            stateResult: stateResult,
            stateTaxLines: stateTaxLines,
            stateIncomeTotal: stateIncomeTotal,
            stateAssessmentTotal: stateAssessmentTotal,
            stateName: input.State,
            taxYear: input.TaxYear,
            stateGross: gross,
            preTaxReducingStateWages: preTaxState,
            net: RoundMoney(net));

        return new PaycheckResult
        {
            GrossPay = RoundMoney(gross),
            PreTaxDeductions = RoundMoney(preTax),
            PostTaxDeductions = RoundMoney(postTax),
            TaxYear = input.TaxYear,
            State = input.State,
            StateTaxableWages = RoundMoney(stateResult.TaxableWages),
            StateTaxLines = stateTaxLines,
            StateWithholding = stateIncomeTotal,
            StateDisabilityInsurance = stateAssessmentTotal,
            StateDisabilityInsuranceLabel = AssessmentLabel(stateTaxLines, stateResult),
            FicaTaxableWages = RoundMoney(ficaWages),
            SocialSecurityWithholding = RoundMoney(ss),
            MedicareWithholding = RoundMoney(medicare),
            AdditionalMedicareWithholding = RoundMoney(addl),
            FederalTaxableIncome = RoundMoney(fedTaxable),
            FederalWithholding = RoundMoney(federal),
            NetPay = RoundMoney(net),
            Explanation = explanation
        };
    }

    private PaycheckExplanation BuildExplanation(
        decimal grossPay,
        PayType payType,
        decimal salaryAmount,
        SalaryBasis salaryBasis,
        int payPeriods,
        decimal regularHours,
        decimal hourlyRate,
        decimal overtimeHours,
        decimal overtimeMultiplier,
        decimal preTax,
        decimal postTax,
        decimal federalTaxableIncome,
        decimal federalPreTaxReducing,
        decimal ficaTaxableWages,
        decimal ficaPreTaxReducing,
        decimal federalWithholding,
        LineExplanation federalExplanation,
        FicaCalculationResult ficaDetail,
        StateWithholdingResult stateResult,
        IReadOnlyList<StateTaxLine> stateTaxLines,
        decimal stateIncomeTotal,
        decimal stateAssessmentTotal,
        UsState stateName,
        int taxYear,
        decimal stateGross,
        decimal preTaxReducingStateWages,
        decimal net)
    {
        var stateRuleIds = RuleIds(stateName, taxYear, TaxRuleScope.RegularWithholding);
        var lines = new List<LineExplanation>
        {
            BuildGrossExplanation(grossPay, payType, salaryAmount, salaryBasis, payPeriods,
                regularHours, hourlyRate, overtimeHours, overtimeMultiplier),
            BuildFederalTaxableIncomeExplanation(grossPay, federalPreTaxReducing, federalTaxableIncome),
            BuildFicaTaxableIncomeExplanation(grossPay, ficaPreTaxReducing, ficaTaxableWages),
            AttachSources(BuildStateTaxableIncomeExplanation(
                stateName, grossPay, RoundMoney(preTaxReducingStateWages),
                RoundMoney(stateResult.TaxableWages), stateResult.Description), stateRuleIds),
            AttachSources(federalExplanation, RuleIds("US", taxYear, TaxRuleScope.FederalWithholding)),
            AttachSources(ficaDetail.SocialSecurityExplanation, RuleIds("US", taxYear, TaxRuleScope.SocialSecurityMedicare)),
            AttachSources(ficaDetail.MedicareExplanation, RuleIds("US", taxYear, TaxRuleScope.SocialSecurityMedicare)),
        };

        if (ficaDetail.AdditionalMedicare > 0m)
        {
            lines.Add(AttachSources(
                ficaDetail.AdditionalMedicareExplanation,
                RuleIds("US", taxYear, TaxRuleScope.AdditionalMedicare)));
        }

        var assessmentRuleIds = RuleIds(stateName, taxYear, TaxRuleScope.PayrollAssessment);
        var localRuleIds = RuleIds(stateName, taxYear, TaxRuleScope.LocalWithholding);
        foreach (var line in stateTaxLines)
        {
            // Siblings of the same kind need a discriminator so each keeps its own
            // breakdown; a lone line stays unkeyed so existing lookups are unchanged.
            var hasSiblings = stateTaxLines.Count(other => other.Kind == line.Kind) > 1;
            lines.Add(AttachSources(
                BuildStateTaxLineExplanation(
                    line, stateResult, stateName, stateGross, preTaxReducingStateWages,
                    subKey: hasSiblings ? line.ExplanationSubKey : null),
                line.Kind switch
                {
                    StateTaxLineKind.PayrollAssessment => assessmentRuleIds,
                    StateTaxLineKind.CountyIncome or StateTaxLineKind.LocalIncome => localRuleIds,
                    _ => stateRuleIds
                }));
        }

        lines.Add(BuildNetExplanation(grossPay, preTax, postTax, federalWithholding,
            ficaDetail.SocialSecurity, ficaDetail.Medicare, ficaDetail.AdditionalMedicare,
            stateIncomeTotal, stateAssessmentTotal, net));

        return new PaycheckExplanation(lines, _sourceCatalog);
    }

    private static decimal SumLines(IReadOnlyList<StateTaxLine> lines, bool assessments) =>
        lines.Where(l => (l.Kind == StateTaxLineKind.PayrollAssessment) == assessments)
             .Sum(l => l.Amount);

    private static string AssessmentLabel(
        IReadOnlyList<StateTaxLine> lines,
        StateWithholdingResult stateResult)
    {
        var assessments = lines.Where(l => l.Kind == StateTaxLineKind.PayrollAssessment).ToList();
        return assessments.Count switch
        {
            0 => stateResult.DisabilityInsuranceLabel,
            1 => assessments[0].Label,
            // The legacy scalar carries a single label, so several programs collapse
            // into one heading; the itemized lines keep each program's own name.
            _ => "State Disability & Paid Leave"
        };
    }

    private IReadOnlyList<string> RuleIds(UsState state, int taxYear, TaxRuleScope scope) =>
        _sourceCatalog?.RuleIds(state, taxYear, scope, TaxCalculationMode.Standard)
        ?? Array.Empty<string>();

    private IReadOnlyList<string> RuleIds(string jurisdiction, int taxYear, TaxRuleScope scope) =>
        _sourceCatalog?.RuleIds(jurisdiction, taxYear, scope, TaxCalculationMode.Standard)
        ?? Array.Empty<string>();

    private static LineExplanation AttachSources(
        LineExplanation line,
        IReadOnlyList<string> ruleIds) =>
        ruleIds.Count == 0 ? line : line with { SourceRuleIds = ruleIds };

    private static LineExplanation BuildGrossExplanation(
        decimal grossPay, PayType payType, decimal salaryAmount, SalaryBasis salaryBasis, int payPeriods,
        decimal regularHours, decimal hourlyRate,
        decimal overtimeHours, decimal overtimeMultiplier)
    {
        if (payType == PayType.Salary)
        {
            return BuildSalaryGrossExplanation(grossPay, salaryAmount, salaryBasis, payPeriods);
        }

        var steps = new List<ExplanationStep>
        {
            new("Regular pay",
                "Regular hours times your hourly rate.",
                regularHours * hourlyRate,
                $"{regularHours:0.##} hrs × {Money(hourlyRate)} = {Money(regularHours * hourlyRate)}"),
        };

        if (overtimeHours > 0m)
        {
            var otAmount = overtimeHours * hourlyRate * overtimeMultiplier;
            steps.Add(new ExplanationStep(
                "Overtime pay",
                "Overtime hours times your hourly rate times the overtime multiplier.",
                otAmount,
                $"{overtimeHours:0.##} hrs × {Money(hourlyRate)} × {overtimeMultiplier:0.##} = {Money(otAmount)}"));
        }

        steps.Add(new ExplanationStep(
            "Gross pay",
            "Total before any deductions or taxes.",
            grossPay,
            $"= {Money(grossPay)}"));

        return new LineExplanation(
            ExplanationLineKey.GrossPay,
            "Gross Pay",
            grossPay,
            steps);
    }

    private static LineExplanation BuildSalaryGrossExplanation(
        decimal grossPay, decimal salaryAmount, SalaryBasis salaryBasis, int payPeriods)
    {
        var steps = new List<ExplanationStep>();

        if (salaryBasis == SalaryBasis.PerYear)
        {
            steps.Add(new ExplanationStep(
                "Annual salary",
                "Your gross salary for the full year, before any deductions or taxes.",
                salaryAmount,
                $"= {Money(salaryAmount)}"));
            steps.Add(new ExplanationStep(
                $"Divide by pay periods ({payPeriods}/year)",
                "Annual salary divided by the number of pay periods in the year gives the gross pay for this period.",
                grossPay,
                $"{Money(salaryAmount)} ÷ {payPeriods} = {Money(grossPay)}"));
        }
        else
        {
            steps.Add(new ExplanationStep(
                "Gross pay per period",
                "The gross amount entered for a single pay period, before any deductions or taxes.",
                grossPay,
                $"= {Money(grossPay)}"));
        }

        return new LineExplanation(
            ExplanationLineKey.GrossPay,
            "Gross Pay",
            grossPay,
            steps);
    }

    private static LineExplanation BuildFederalTaxableIncomeExplanation(
        decimal grossPay, decimal preTaxReducing, decimal taxableIncome)
    {
        var steps = new List<ExplanationStep>
        {
            new("Gross pay",
                "Total earnings before any deductions or taxes.",
                grossPay,
                $"= {Money(grossPay)}"),
        };

        if (preTaxReducing > 0m)
        {
            steps.Add(new ExplanationStep(
                "Less pre-tax deductions reducing federal wages",
                "Pre-tax items such as traditional 401(k)/403(b) and Section 125 medical lower the wages subject to federal income tax. Roth and other after-tax deductions do not.",
                preTaxReducing,
                $"− {Money(preTaxReducing)}"));
        }

        steps.Add(new ExplanationStep(
            "Federal taxable income",
            "The wage base the IRS percentage-method withholding formula is applied to.",
            taxableIncome,
            $"= {Money(taxableIncome)}"));

        return new LineExplanation(
            ExplanationLineKey.FederalTaxableIncome,
            "Federal Taxable Income",
            taxableIncome,
            steps,
            "IRS Publication 15-T (2026). Pre-tax 401(k)/Section 125 deductions reduce federally taxable wages.");
    }

    private static LineExplanation BuildFicaTaxableIncomeExplanation(
        decimal grossPay, decimal preTaxReducing, decimal taxableWages)
    {
        var steps = new List<ExplanationStep>
        {
            new("Gross pay",
                "Total earnings before any deductions or taxes.",
                grossPay,
                $"= {Money(grossPay)}"),
        };

        if (preTaxReducing > 0m)
        {
            steps.Add(new ExplanationStep(
                "Less pre-tax deductions reducing FICA wages",
                "Only Section 125 cafeteria-plan benefits (such as pre-tax medical) reduce Social Security and Medicare wages. 401(k)/403(b) contributions do not.",
                preTaxReducing,
                $"− {Money(preTaxReducing)}"));
        }

        steps.Add(new ExplanationStep(
            "FICA taxable wages",
            "The wage base for Social Security and Medicare (FICA) taxes.",
            taxableWages,
            $"= {Money(taxableWages)}"));

        return new LineExplanation(
            ExplanationLineKey.FicaTaxableWages,
            "FICA Taxable Income",
            taxableWages,
            steps,
            "FICA wages — Section 125 benefits reduce them; 401(k)/403(b) contributions do not.");
    }

    private static LineExplanation BuildStateTaxableIncomeExplanation(
        UsState state, decimal grossPay, decimal preTaxReducing, decimal taxableWages, string? description)
    {
        // No-income-tax states (and fully exempt wages) report zero taxable wages
        // even when gross pay is positive, so the gross − deductions arithmetic
        // would not add up. Show a single informational step instead.
        if (taxableWages == 0m && grossPay - preTaxReducing > 0m)
        {
            var noTaxSteps = new List<ExplanationStep>
            {
                new("No state taxable wages",
                    string.IsNullOrEmpty(description)
                        ? $"{state} does not tax these wages for income-tax purposes, so the state taxable wage base is zero."
                        : description,
                    0m,
                    $"= {Money(0m)}"),
            };
            return new LineExplanation(
                ExplanationLineKey.StateTaxableWages,
                $"State Taxable Income ({state})",
                0m,
                noTaxSteps,
                $"{state} state taxable wage rules (2026).");
        }

        var steps = new List<ExplanationStep>
        {
            new("Gross pay",
                "Total earnings before any deductions or taxes.",
                grossPay,
                $"= {Money(grossPay)}"),
        };

        if (preTaxReducing > 0m)
        {
            steps.Add(new ExplanationStep(
                "Less pre-tax deductions reducing state wages",
                "Pre-tax items the state recognizes (such as traditional 401(k) or Section 125 medical) lower the wages subject to state income tax.",
                preTaxReducing,
                $"− {Money(preTaxReducing)}"));
        }

        steps.Add(new ExplanationStep(
            "State taxable wages",
            "The base the state's withholding formula is applied to.",
            taxableWages,
            $"= {Money(taxableWages)}"));

        return new LineExplanation(
            ExplanationLineKey.StateTaxableWages,
            $"State Taxable Income ({state})",
            taxableWages,
            steps,
            $"{state} state taxable wage rules (2026).");
    }

    private static ExplanationLineKey KeyFor(StateTaxLineKind kind) => kind switch
    {
        StateTaxLineKind.CountyIncome => ExplanationLineKey.StateCountyIncome,
        StateTaxLineKind.LocalIncome => ExplanationLineKey.StateLocalIncome,
        StateTaxLineKind.PayrollAssessment => ExplanationLineKey.StateDisability,
        _ => ExplanationLineKey.StateWithholding
    };

    private static LineExplanation BuildStateTaxLineExplanation(
        StateTaxLine line,
        StateWithholdingResult stateResult,
        UsState state,
        decimal stateGross,
        decimal preTaxReducingStateWages,
        string? subKey)
    {
        var key = KeyFor(line.Kind);
        var title = line.Kind == StateTaxLineKind.StateIncome
            ? $"{line.Label} ({state})"
            : line.Label;

        // Calculators that opt in supply the full worksheet narrative themselves.
        if (line.Steps is { Count: > 0 })
        {
            return new LineExplanation(
                key, title, line.Amount, line.Steps,
                line.Reference ?? DefaultReference(line.Kind, state),
                SubKey: subKey);
        }

        var steps = line.Kind == StateTaxLineKind.StateIncome
            ? BuildFallbackIncomeSteps(line, stateResult, state, stateGross, preTaxReducingStateWages)
            : new List<ExplanationStep>
            {
                new(line.Label,
                    string.IsNullOrEmpty(stateResult.Description)
                        ? $"{state} mandates this line in addition to state income tax."
                        : stateResult.Description,
                    line.Amount,
                    $"= {Money(line.Amount)}")
            };

        return new LineExplanation(
            key, title, line.Amount, steps,
            line.Reference ?? DefaultReference(line.Kind, state),
            SubKey: subKey);
    }

    private static string DefaultReference(StateTaxLineKind kind, UsState state) => kind switch
    {
        StateTaxLineKind.CountyIncome => $"{state} county income-tax rules (2026).",
        StateTaxLineKind.LocalIncome => $"{state} local income-tax rules (2026).",
        StateTaxLineKind.PayrollAssessment => $"{state} state disability / leave insurance rules (2026).",
        _ => $"{state} state withholding rules (2026)."
    };

    private static List<ExplanationStep> BuildFallbackIncomeSteps(
        StateTaxLine line,
        StateWithholdingResult stateResult,
        UsState state,
        decimal stateGross,
        decimal preTaxReducingStateWages)
    {
        var steps = new List<ExplanationStep>();

        if (preTaxReducingStateWages > 0m)
        {
            steps.Add(new ExplanationStep(
                "Gross wages this period",
                "Starting wages before state-deductible pre-tax items are removed.",
                stateGross,
                $"= {Money(stateGross)}"));
            steps.Add(new ExplanationStep(
                "Less pre-tax deductions reducing state wages",
                "Pre-tax items like traditional 401(k) or Section 125 medical reduce state taxable wages.",
                preTaxReducingStateWages,
                $"− {Money(preTaxReducingStateWages)}"));
        }

        steps.Add(new ExplanationStep(
            "State taxable wages",
            "The base the state's withholding formula is applied to.",
            stateResult.TaxableWages,
            $"= {Money(stateResult.TaxableWages)}"));

        steps.Add(new ExplanationStep(
            $"{state} state withholding",
            string.IsNullOrEmpty(stateResult.Description)
                ? $"Computed by the {state} state withholding calculator using your filing inputs."
                : stateResult.Description,
            line.Amount,
            $"= {Money(line.Amount)}"));

        return steps;
    }

    private static LineExplanation BuildNetExplanation(
        decimal grossPay, decimal preTax, decimal postTax,
        decimal federal, decimal ss, decimal medicare, decimal addlMedicare,
        decimal stateWh, decimal stateDi,
        decimal net)
    {
        var totalTaxes = federal + ss + medicare + addlMedicare + stateWh + stateDi;
        var steps = new List<ExplanationStep>
        {
            new("Gross pay", "Total before deductions and taxes.", grossPay, $"= {Money(grossPay)}"),
            new("Less pre-tax deductions", "Subtracted from gross before some taxes are computed.", preTax, $"− {Money(preTax)}"),
            new("Less total taxes",
                "Sum of federal, FICA (Social Security + Medicare + Additional Medicare), and state taxes.",
                totalTaxes,
                $"− {Money(totalTaxes)}"),
            new("Less post-tax deductions", "Reduce net pay only — they don't change any tax base.", postTax, $"− {Money(postTax)}"),
            new("Net pay", "Take-home amount for this period.", net, $"= {Money(net)}"),
        };
        return new LineExplanation(
            ExplanationLineKey.NetPay,
            "Net Pay",
            net,
            steps);
    }

    private static string Money(decimal v) => v.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

    private static decimal RoundMoney(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
