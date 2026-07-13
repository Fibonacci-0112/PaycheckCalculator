using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Fica;
using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Core.Pay;

/// <summary>
/// Computes a self-employment / 1099 contractor's federal self-employment (SE) tax and
/// an estimate of state income tax on annual net earnings, then derives take-home and a
/// quarterly estimated-payment schedule (Form 1040-ES).
///
/// <para>
/// SE tax is a <b>federal</b> tax — the self-employed pay <i>both</i> the employer and
/// employee halves of FICA: 12.4% Social Security (capped at the annual wage base) plus
/// 2.9% Medicare (uncapped), with the extra 0.9% Additional Medicare above $200,000. It
/// is applied to 92.35% of net earnings (the deduction-for-the-employer-half adjustment).
/// </para>
///
/// <para>
/// No U.S. state levies its own self-employment tax; states tax self-employment income as
/// ordinary income. The state portion is therefore estimated by running the full net
/// earnings through the same per-state income-tax engine that powers a normal paycheck
/// (<see cref="StateCalculatorRegistry"/>), at an annual frequency. State disability /
/// paid-leave levies (e.g. CA SDI) are wage-based payroll taxes and are <i>not</i> applied
/// to self-employment income.
/// </para>
/// </summary>
public sealed class SelfEmploymentCalculator
{
    /// <summary>Combined (employer + employee) Social Security rate for the self-employed.</summary>
    public const decimal SocialSecurityRate = 0.124m;

    /// <summary>Combined (employer + employee) Medicare rate for the self-employed.</summary>
    public const decimal MedicareRate = 0.029m;

    /// <summary>Additional Medicare tax rate on earnings above the threshold.</summary>
    public const decimal AdditionalMedicareRate = 0.009m;

    /// <summary>Share of net earnings subject to SE tax (the 7.65% employer-half adjustment).</summary>
    public const decimal NetEarningsMultiplier = 0.9235m;

    private readonly StateCalculatorRegistry _stateRegistry;
    private readonly decimal _socialSecurityWageBase;
    private readonly decimal _additionalMedicareThreshold;

    public SelfEmploymentCalculator(StateCalculatorRegistry stateRegistry, FicaCalculator fica)
    {
        ArgumentNullException.ThrowIfNull(stateRegistry);
        ArgumentNullException.ThrowIfNull(fica);
        _stateRegistry = stateRegistry;
        // Share the FICA wage base / Additional-Medicare threshold so SE tax stays in sync.
        _socialSecurityWageBase = fica.SocialSecurityWageBase;
        _additionalMedicareThreshold = fica.AdditionalMedicareEmployerThreshold;
    }

    public SelfEmploymentResult Calculate(SelfEmploymentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.AnnualNetEarnings < 0m)
            throw new ArgumentOutOfRangeException(
                nameof(input), input.AnnualNetEarnings, "Annual net earnings cannot be negative.");
        if (!TaxYearSupport.IsSupported(input.TaxYear))
            throw new NotSupportedException(
                $"Tax year {input.TaxYear} is not supported. Only {TaxYearSupport.Default} tax data is currently loaded.");

        var earnings = RoundMoney(input.AnnualNetEarnings);

        // 1) Net earnings subject to SE tax = 92.35% of net profit.
        var seBase = earnings * NetEarningsMultiplier;

        // 2) Social Security portion (12.4%), limited to the remaining annual wage base.
        var remainingSsBase = Math.Max(0m, _socialSecurityWageBase - input.YtdSocialSecurityWages);
        var ssTaxable = Math.Min(seBase, remainingSsBase);
        var ss = ssTaxable * SocialSecurityRate;

        // 3) Medicare portion (2.9%), no cap.
        var medicare = seBase * MedicareRate;

        // 4) Additional Medicare (0.9%) on SE earnings above the $200,000 threshold,
        //    crossing it with any wages already earned this year.
        var prior = input.YtdMedicareWages;
        var current = input.YtdMedicareWages + seBase;
        var over = Math.Max(0m, current - _additionalMedicareThreshold)
                 - Math.Max(0m, prior - _additionalMedicareThreshold);
        var addl = over * AdditionalMedicareRate;

        var ssR = RoundMoney(ss);
        var medicareR = RoundMoney(medicare);
        var addlR = RoundMoney(addl);
        var seTax = ssR + medicareR + addlR;

        // 5) State income tax estimate — run the full net earnings through the state's
        //    ordinary income-tax engine at an annual frequency. No separate state SE tax
        //    exists; state disability/leave levies do not apply to self-employment income.
        var stateCalc = _stateRegistry.GetCalculator(input.State);
        var stateContext = new CommonWithholdingContext(
            input.State,
            GrossWages: earnings,
            PayPeriod: PayFrequency.Annual,
            Year: input.TaxYear,
            PreTaxDeductionsReducingStateWages: 0m,
            FederalWithholdingPerPeriod: 0m);
        var stateValues = input.StateInputValues ?? new StateInputValues();
        var stateResult = stateCalc.Calculate(stateContext, stateValues);
        var stateTax = RoundMoney(stateResult.Withholding);

        var takeHomeBeforeState = earnings - seTax;
        var takeHome = takeHomeBeforeState - stateTax;

        // 6) Quarterly estimated payments — equal installments of each annual figure.
        var (fedQuarters, fedPerQuarter) = SplitIntoQuarters(seTax);
        var (stateQuarters, statePerQuarter) = SplitIntoQuarters(stateTax);
        var quarters = BuildQuarters(fedQuarters, stateQuarters);

        var explanation = BuildExplanation(
            earnings, seBase, ssTaxable, remainingSsBase, ssR, medicareR, over, addlR, seTax,
            input.State, stateResult, stateTax, takeHome, quarters, fedPerQuarter, statePerQuarter);

        return new SelfEmploymentResult
        {
            AnnualNetEarnings = earnings,
            NetEarningsSubjectToSeTax = RoundMoney(seBase),
            TaxYear = input.TaxYear,
            State = input.State,
            SocialSecurityTax = ssR,
            MedicareTax = medicareR,
            AdditionalMedicareTax = addlR,
            SelfEmploymentTax = seTax,
            StateIncomeTax = stateTax,
            StateIncomeTaxDescription = stateResult.Description ?? "",
            TakeHomeBeforeStateTax = RoundMoney(takeHomeBeforeState),
            TakeHome = RoundMoney(takeHome),
            FederalQuarterlyPayment = fedPerQuarter,
            StateQuarterlyPayment = statePerQuarter,
            QuarterlyEstimates = quarters,
            Explanation = explanation
        };
    }

    /// <summary>
    /// Splits an annual amount into four equal whole-cent installments. The first three
    /// use the rounded quarter; the last absorbs any rounding remainder so the four sum
    /// exactly to <paramref name="annual"/>. Returns the four amounts and the per-quarter
    /// (Q1) installment shown as the headline figure.
    /// </summary>
    private static (decimal[] amounts, decimal perQuarter) SplitIntoQuarters(decimal annual)
    {
        var perQuarter = RoundMoney(annual / 4m);
        var amounts = new[] { perQuarter, perQuarter, perQuarter, RoundMoney(annual - perQuarter * 3m) };
        return (amounts, perQuarter);
    }

    private static IReadOnlyList<QuarterlyEstimate> BuildQuarters(decimal[] federal, decimal[] state)
    {
        // Standard IRS Form 1040-ES installment due dates for the 2026 tax year; most
        // states align their estimated-payment deadlines with these.
        var schedule = new (string Label, string Period, DateOnly Due)[]
        {
            ("Q1", "Jan 1 – Mar 31, 2026",  new DateOnly(2026, 4, 15)),
            ("Q2", "Apr 1 – May 31, 2026",  new DateOnly(2026, 6, 15)),
            ("Q3", "Jun 1 – Aug 31, 2026",  new DateOnly(2026, 9, 15)),
            ("Q4", "Sep 1 – Dec 31, 2026",  new DateOnly(2027, 1, 15)),
        };

        var quarters = new List<QuarterlyEstimate>(4);
        for (var i = 0; i < 4; i++)
            quarters.Add(new QuarterlyEstimate(
                schedule[i].Label, schedule[i].Period, schedule[i].Due, federal[i], state[i]));
        return quarters;
    }

    private PaycheckExplanation BuildExplanation(
        decimal earnings, decimal seBase, decimal ssTaxable, decimal remainingSsBase,
        decimal ss, decimal medicare, decimal addlOver, decimal addl, decimal seTax,
        UsState state, StateWithholdingResult stateResult, decimal stateTax,
        decimal takeHome, IReadOnlyList<QuarterlyEstimate> quarters,
        decimal fedPerQuarter, decimal statePerQuarter)
    {
        var lines = new List<LineExplanation>
        {
            new(ExplanationLineKey.GrossPay,
                "Net Self-Employment Income",
                earnings,
                new List<ExplanationStep>
                {
                    new("Annual net profit",
                        "Your Schedule C net profit for the year — gross receipts minus business expenses. Self-employment tax is figured from this.",
                        earnings,
                        $"= {Money(earnings)}"),
                }),
            new(ExplanationLineKey.FicaTaxableWages,
                "Net Earnings Subject to SE Tax",
                RoundMoney(seBase),
                new List<ExplanationStep>
                {
                    new("Net profit", "Starting point for the SE tax base.", earnings, $"= {Money(earnings)}"),
                    new("Multiply by 92.35%",
                        "Only 92.35% of net earnings is subject to SE tax — this stands in for the employer half of FICA, which a business could deduct.",
                        RoundMoney(seBase),
                        $"{Money(earnings)} × {NetEarningsMultiplier:0.####} = {Money(RoundMoney(seBase))}"),
                },
                "IRS Schedule SE (2026)."),
            new(ExplanationLineKey.SocialSecurity,
                "Social Security (12.4%)",
                ss,
                new List<ExplanationStep>
                {
                    new("Net earnings subject to SE tax", "The 92.35% base.", RoundMoney(seBase), $"= {Money(RoundMoney(seBase))}"),
                    new("Remaining Social Security wage base",
                        $"Only the first {Money(_socialSecurityWageBase)} of earnings is subject to the 12.4% Social Security tax each year.",
                        RoundMoney(remainingSsBase),
                        $"= {Money(RoundMoney(remainingSsBase))}"),
                    new("Earnings taxed for Social Security", "The smaller of the SE base or the remaining wage base.", RoundMoney(ssTaxable), $"min = {Money(RoundMoney(ssTaxable))}"),
                    new("Apply 12.4%", "Employer + employee Social Security halves combined.", ss, $"{Money(RoundMoney(ssTaxable))} × {SocialSecurityRate:P1} = {Money(ss)}"),
                },
                "Self-Employment Contributions Act — Social Security portion (2026)."),
            new(ExplanationLineKey.Medicare,
                "Medicare (2.9%)",
                medicare,
                new List<ExplanationStep>
                {
                    new("Net earnings subject to SE tax", "Medicare has no wage cap.", RoundMoney(seBase), $"= {Money(RoundMoney(seBase))}"),
                    new("Apply 2.9%", "Employer + employee Medicare halves combined.", medicare, $"{Money(RoundMoney(seBase))} × {MedicareRate:P1} = {Money(medicare)}"),
                },
                "Self-Employment Contributions Act — Medicare portion (2026)."),
        };

        if (addl > 0m)
        {
            lines.Add(new LineExplanation(
                ExplanationLineKey.AdditionalMedicare,
                "Additional Medicare (0.9%)",
                addl,
                new List<ExplanationStep>
                {
                    new("Earnings above the $200,000 threshold", "Net SE earnings (plus any other Medicare wages) over $200,000.", RoundMoney(addlOver), $"= {Money(RoundMoney(addlOver))}"),
                    new("Apply 0.9%", "Additional Medicare tax on the excess.", addl, $"{Money(RoundMoney(addlOver))} × {AdditionalMedicareRate:P1} = {Money(addl)}"),
                },
                "Additional Medicare Tax — IRC §1401(b)(2)."));
        }

        // The "Self-Employment Tax" total reuses the federal-withholding key — SE tax is
        // the federal tax on these earnings, and this mode has no income-tax withholding row.
        lines.Add(new LineExplanation(
            ExplanationLineKey.FederalWithholding,
            "Self-Employment Tax",
            seTax,
            new List<ExplanationStep>
            {
                new("Social Security", "12.4% portion.", ss, $"= {Money(ss)}"),
                new("Medicare", "2.9% portion.", medicare, $"+ {Money(medicare)}"),
                new("Additional Medicare", "0.9% above $200,000.", addl, $"+ {Money(addl)}"),
                new("Total self-employment tax", "Paid to the IRS with your federal return / estimated payments.", seTax, $"= {Money(seTax)}"),
            },
            "IRS Schedule SE (2026). 15.3% combined (12.4% Social Security + 2.9% Medicare)."));

        lines.Add(BuildStateLine(state, earnings, stateResult, stateTax));

        lines.Add(new LineExplanation(
            ExplanationLineKey.NetPay,
            "Take-Home (after tax)",
            takeHome,
            new List<ExplanationStep>
            {
                new("Net profit", "Annual net self-employment income.", earnings, $"= {Money(earnings)}"),
                new("Less self-employment tax", "Federal Social Security + Medicare.", seTax, $"− {Money(seTax)}"),
                new("Less estimated state income tax", "Ordinary state income tax on the earnings.", stateTax, $"− {Money(stateTax)}"),
                new("Take-home", "What's left after SE tax and estimated state income tax (before federal income tax).", takeHome, $"= {Money(takeHome)}"),
            }));

        return new PaycheckExplanation(lines);
    }

    private LineExplanation BuildStateLine(UsState state, decimal earnings, StateWithholdingResult stateResult, decimal stateTax)
    {
        var steps = new List<ExplanationStep>();
        if (stateResult.WithholdingSteps is { Count: > 0 })
        {
            steps.AddRange(stateResult.WithholdingSteps);
        }
        else if (stateTax == 0m)
        {
            steps.Add(new ExplanationStep(
                "No state income tax",
                string.IsNullOrEmpty(stateResult.Description)
                    ? $"{state} does not tax this income, so there is no state income tax on these earnings."
                    : stateResult.Description,
                0m,
                $"= {Money(0m)}"));
        }
        else
        {
            steps.Add(new ExplanationStep("Net earnings", "State income tax is on ordinary income — your full net profit.", earnings, $"= {Money(earnings)}"));
            steps.Add(new ExplanationStep($"{state} income tax", string.IsNullOrEmpty(stateResult.Description) ? "Estimated using the state's income-tax rules." : stateResult.Description, stateTax, $"= {Money(stateTax)}"));
        }

        return new LineExplanation(
            ExplanationLineKey.StateWithholding,
            $"Estimated State Income Tax ({state})",
            stateTax,
            steps,
            $"{state} income tax on self-employment income (estimate, 2026). No state levies a separate self-employment tax.");
    }

    private static string Money(decimal v) => v.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

    private static decimal RoundMoney(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
