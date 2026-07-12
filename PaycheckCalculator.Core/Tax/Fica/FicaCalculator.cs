using PaycheckCalculator.Core.Explanation;

namespace PaycheckCalculator.Core.Tax.Fica;

public sealed class FicaCalculator
{
    public const decimal SocialSecurityRate = 0.062m;
    public const decimal MedicareRate = 0.0145m;
    public const decimal AdditionalMedicareRate = 0.009m;

    public decimal SocialSecurityWageBase { get; init; } = 184_500m;
    public decimal AdditionalMedicareEmployerThreshold { get; init; } = 200_000m;

    public (decimal ss, decimal medicare, decimal addlMedicare) Calculate(decimal medicareWagesThisPeriod, decimal ytdSsWages, decimal ytdMedicareWages)
    {
        var detailed = CalculateWithExplanation(medicareWagesThisPeriod, ytdSsWages, ytdMedicareWages);
        return (detailed.SocialSecurity, detailed.Medicare, detailed.AdditionalMedicare);
    }

    /// <summary>
    /// Computes Social Security, Medicare, and Additional Medicare withholding
    /// alongside a step-by-step explanation for each.
    /// </summary>
    public FicaCalculationResult CalculateWithExplanation(
        decimal medicareWagesThisPeriod,
        decimal ytdSsWages,
        decimal ytdMedicareWages)
    {
        // ── Social Security ─────────────────────────────────
        var ssSteps = new List<ExplanationStep>();
        ssSteps.Add(new ExplanationStep(
            "FICA taxable wages this period",
            "Gross wages reduced by Section 125 pre-tax benefits (e.g. medical, HSA, FSA). 401(k) does NOT reduce FICA wages.",
            medicareWagesThisPeriod,
            $"= {Money(medicareWagesThisPeriod)}"));

        var remainingSsBase = Math.Max(0m, SocialSecurityWageBase - ytdSsWages);
        ssSteps.Add(new ExplanationStep(
            "Remaining Social Security wage base",
            $"Each year only the first {Money(SocialSecurityWageBase)} of wages is subject to SS tax. Subtract year-to-date SS wages from the cap.",
            remainingSsBase,
            $"max(0, {Money(SocialSecurityWageBase)} − {Money(ytdSsWages)}) = {Money(remainingSsBase)}"));

        var ssTaxable = Math.Min(medicareWagesThisPeriod, remainingSsBase);
        ssSteps.Add(new ExplanationStep(
            "Wages subject to Social Security this period",
            "The smaller of this period's wages or the remaining wage base.",
            ssTaxable,
            $"min({Money(medicareWagesThisPeriod)}, {Money(remainingSsBase)}) = {Money(ssTaxable)}"));

        var ss = ssTaxable * SocialSecurityRate;
        ssSteps.Add(new ExplanationStep(
            "Apply Social Security rate (6.2%)",
            "Employee share of Social Security tax.",
            ss,
            $"{Money(ssTaxable)} × {SocialSecurityRate:P1} = {Money(ss)}"));

        var ssExplanation = new LineExplanation(
            ExplanationLineKey.SocialSecurity,
            "Social Security Tax",
            ss,
            ssSteps,
            "FICA — IRC §3101(a). 2026 wage base $184,500.");

        // ── Medicare (regular 1.45%) ────────────────────────
        var medicareSteps = new List<ExplanationStep>();
        medicareSteps.Add(new ExplanationStep(
            "FICA taxable wages this period",
            "Medicare has no wage base — all FICA-taxable wages are subject to the 1.45% rate.",
            medicareWagesThisPeriod,
            $"= {Money(medicareWagesThisPeriod)}"));

        var medicare = medicareWagesThisPeriod * MedicareRate;
        medicareSteps.Add(new ExplanationStep(
            "Apply Medicare rate (1.45%)",
            "Employee share of regular Medicare tax.",
            medicare,
            $"{Money(medicareWagesThisPeriod)} × {MedicareRate:P2} = {Money(medicare)}"));

        var medicareExplanation = new LineExplanation(
            ExplanationLineKey.Medicare,
            "Medicare Tax",
            medicare,
            medicareSteps,
            "FICA — IRC §3101(b). Flat 1.45%, no wage base.");

        // ── Additional Medicare (0.9% above $200k) ──────────
        var addlSteps = new List<ExplanationStep>();
        var prior = ytdMedicareWages;
        var current = ytdMedicareWages + medicareWagesThisPeriod;
        var over = Math.Max(0m, current - AdditionalMedicareEmployerThreshold)
                 - Math.Max(0m, prior - AdditionalMedicareEmployerThreshold);
        var addl = over * AdditionalMedicareRate;

        addlSteps.Add(new ExplanationStep(
            "Year-to-date Medicare wages before this period",
            "Tracks how much of the $200,000 employer threshold has already been crossed.",
            prior,
            $"= {Money(prior)}"));
        addlSteps.Add(new ExplanationStep(
            "Year-to-date Medicare wages including this period",
            "Add this period's wages to the YTD total.",
            current,
            $"{Money(prior)} + {Money(medicareWagesThisPeriod)} = {Money(current)}"));
        addlSteps.Add(new ExplanationStep(
            $"Wages above the {Money(AdditionalMedicareEmployerThreshold)} employer threshold",
            "Only wages that cross the $200,000 single-employer threshold trigger the 0.9% Additional Medicare withholding.",
            over,
            $"max(0, {Money(current)} − {Money(AdditionalMedicareEmployerThreshold)}) − max(0, {Money(prior)} − {Money(AdditionalMedicareEmployerThreshold)}) = {Money(over)}"));
        addlSteps.Add(new ExplanationStep(
            "Apply Additional Medicare rate (0.9%)",
            "Employer-side Additional Medicare withholding. The employee may owe more or less when filing Form 8959.",
            addl,
            $"{Money(over)} × {AdditionalMedicareRate:P2} = {Money(addl)}"));

        var addlExplanation = new LineExplanation(
            ExplanationLineKey.AdditionalMedicare,
            "Additional Medicare Tax",
            addl,
            addlSteps,
            "Additional Medicare Tax — IRC §3101(b)(2). 0.9% on wages above $200,000 (single-employer threshold).");

        return new FicaCalculationResult(
            ss,
            medicare,
            addl,
            ssExplanation,
            medicareExplanation,
            addlExplanation);
    }

    private static string Money(decimal v) => v.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
}

/// <summary>FICA result envelope with both the amounts and per-line explanations.</summary>
public sealed record FicaCalculationResult(
    decimal SocialSecurity,
    decimal Medicare,
    decimal AdditionalMedicare,
    LineExplanation SocialSecurityExplanation,
    LineExplanation MedicareExplanation,
    LineExplanation AdditionalMedicareExplanation);
