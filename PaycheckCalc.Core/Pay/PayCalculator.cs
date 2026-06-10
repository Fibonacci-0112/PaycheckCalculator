using PaycheckCalc.Core.Explanation;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Pay;

public sealed class PayCalculator
{
    private readonly StateCalculatorRegistry _stateRegistry;
    private readonly FicaCalculator _fica;
    private readonly Irs15TPercentageCalculator _fed;

    public PayCalculator(
        StateCalculatorRegistry stateRegistry,
        FicaCalculator fica,
        Irs15TPercentageCalculator fed)
    {
        _stateRegistry = stateRegistry;
        _fica = fica;
        _fed = fed;
    }

    public PaycheckResult Calculate(PaycheckInput input)
    {
        var gross = (input.RegularHours * input.HourlyRate)
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
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxState,
            FederalWithholdingPerPeriod: RoundMoney(federal));
        var stateValues = input.StateInputValues ?? new StateInputValues();
        var stateResult = calc.Calculate(context, stateValues);

        var net = gross - preTax - postTax
                - stateResult.Withholding - stateResult.DisabilityInsurance
                - ss - medicare - addl - federal;

        var explanation = BuildExplanation(
            grossPay: RoundMoney(gross),
            regularHours: input.RegularHours,
            hourlyRate: input.HourlyRate,
            overtimeHours: input.OvertimeHours,
            overtimeMultiplier: input.OvertimeMultiplier,
            preTax: RoundMoney(preTax),
            postTax: RoundMoney(postTax),
            federalWithholding: RoundMoney(federal),
            federalExplanation: fedDetail.Explanation,
            ficaDetail: ficaDetail,
            stateResult: stateResult,
            stateName: input.State,
            stateGross: gross,
            preTaxReducingStateWages: preTaxState,
            net: RoundMoney(net));

        return new PaycheckResult
        {
            GrossPay = RoundMoney(gross),
            PreTaxDeductions = RoundMoney(preTax),
            PostTaxDeductions = RoundMoney(postTax),
            State = input.State,
            StateTaxableWages = RoundMoney(stateResult.TaxableWages),
            StateWithholding = RoundMoney(stateResult.Withholding),
            StateDisabilityInsurance = RoundMoney(stateResult.DisabilityInsurance),
            StateDisabilityInsuranceLabel = stateResult.DisabilityInsuranceLabel,
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

    private static PaycheckExplanation BuildExplanation(
        decimal grossPay,
        decimal regularHours,
        decimal hourlyRate,
        decimal overtimeHours,
        decimal overtimeMultiplier,
        decimal preTax,
        decimal postTax,
        decimal federalWithholding,
        LineExplanation federalExplanation,
        FicaCalculationResult ficaDetail,
        StateWithholdingResult stateResult,
        UsState stateName,
        decimal stateGross,
        decimal preTaxReducingStateWages,
        decimal net)
    {
        var lines = new List<LineExplanation>
        {
            BuildGrossExplanation(grossPay, regularHours, hourlyRate, overtimeHours, overtimeMultiplier),
            federalExplanation,
            ficaDetail.SocialSecurityExplanation,
            ficaDetail.MedicareExplanation,
        };

        if (ficaDetail.AdditionalMedicare > 0m)
        {
            lines.Add(ficaDetail.AdditionalMedicareExplanation);
        }

        lines.Add(BuildStateExplanation(stateResult, stateName, stateGross, preTaxReducingStateWages));

        if (stateResult.DisabilityInsurance > 0m)
        {
            lines.Add(BuildStateDisabilityExplanation(stateResult, stateName));
        }

        lines.Add(BuildNetExplanation(grossPay, preTax, postTax, federalWithholding,
            ficaDetail.SocialSecurity, ficaDetail.Medicare, ficaDetail.AdditionalMedicare,
            stateResult.Withholding, stateResult.DisabilityInsurance, net));

        return new PaycheckExplanation(lines);
    }

    private static LineExplanation BuildGrossExplanation(
        decimal grossPay, decimal regularHours, decimal hourlyRate,
        decimal overtimeHours, decimal overtimeMultiplier)
    {
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

    private static LineExplanation BuildStateExplanation(
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
            stateResult.Withholding,
            $"= {Money(stateResult.Withholding)}"));

        return new LineExplanation(
            ExplanationLineKey.StateWithholding,
            $"State Income Tax ({state})",
            stateResult.Withholding,
            steps,
            $"{state} state withholding rules (2026).");
    }

    private static LineExplanation BuildStateDisabilityExplanation(StateWithholdingResult stateResult, UsState state)
    {
        var steps = new List<ExplanationStep>
        {
            new(stateResult.DisabilityInsuranceLabel,
                string.IsNullOrEmpty(stateResult.Description)
                    ? $"{state} mandates this line in addition to state income tax."
                    : stateResult.Description,
                stateResult.DisabilityInsurance,
                $"= {Money(stateResult.DisabilityInsurance)}"),
        };
        return new LineExplanation(
            ExplanationLineKey.StateDisability,
            stateResult.DisabilityInsuranceLabel,
            stateResult.DisabilityInsurance,
            steps,
            $"{state} state disability / leave insurance rules (2026).");
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
