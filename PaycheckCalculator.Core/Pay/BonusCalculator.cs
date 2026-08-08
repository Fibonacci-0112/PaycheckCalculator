using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.Fica;
using PaycheckCalculator.Core.Tax.Supplemental;
using PaycheckCalculator.Core.Tax.Sources;

namespace PaycheckCalculator.Core.Pay;

/// <summary>
/// Computes take-home pay on a supplemental wage (bonus, commission, award, etc.) using the
/// federal flat-rate method. It composes — and must not absorb — three per-payment steps:
/// federal supplemental withholding (<see cref="FederalSupplementalCalculator"/>), FICA
/// (<see cref="FicaCalculator"/>, with the same wage-base / Additional-Medicare handling as
/// a normal paycheck), and state supplemental withholding
/// (<see cref="StateSupplementalCalculator"/>). The result is the net bonus plus a
/// "Show Your Work" breakdown for each line.
/// </summary>
public sealed class BonusCalculator
{
    private readonly FederalSupplementalCalculator _federal;
    private readonly FicaCalculator _fica;
    private readonly StateSupplementalCalculator _state;
    private readonly TaxSourceCatalog? _sourceCatalog;

    public BonusCalculator(
        FederalSupplementalCalculator federal,
        FicaCalculator fica,
        StateSupplementalCalculator state,
        TaxSourceCatalog? sourceCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(federal);
        ArgumentNullException.ThrowIfNull(fica);
        ArgumentNullException.ThrowIfNull(state);
        _federal = federal;
        _fica = fica;
        _state = state;
        _sourceCatalog = sourceCatalog;
    }

    public BonusResult Calculate(BonusInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.BonusAmount < 0m)
            throw new ArgumentOutOfRangeException(
                nameof(input), input.BonusAmount, "Bonus amount cannot be negative.");
        if (!TaxYearSupport.IsSupported(input.TaxYear))
            throw new NotSupportedException(
                $"Tax year {input.TaxYear} is not supported. Only {TaxYearSupport.Default} tax data is currently loaded.");

        var bonus = RoundMoney(input.BonusAmount);

        // 1) Federal flat supplemental withholding (22%; 37% above the cumulative $1M).
        var (federal, federalExplanation) = _federal.CalculateWithExplanation(bonus, input.YtdSupplementalWages);

        // 2) FICA — the bonus is fully FICA-taxable; YTD wages honor the SS cap and the
        //    Additional Medicare threshold just like a regular paycheck.
        var ficaDetail = _fica.CalculateWithExplanation(bonus, input.YtdSocialSecurityWages, input.YtdMedicareWages);
        var ss = ficaDetail.SocialSecurity;
        var medicare = ficaDetail.Medicare;
        var addl = ficaDetail.AdditionalMedicare;

        // 3) State supplemental withholding (flat rate, % of federal, none, or regular method).
        var stateResult = _state.Calculate(input.State, bonus, RoundMoney(federal));

        // Round each component individually, then derive net so the displayed figures add up.
        var federalR = RoundMoney(federal);
        var ssR = RoundMoney(ss);
        var medicareR = RoundMoney(medicare);
        var addlR = RoundMoney(addl);
        var stateR = RoundMoney(stateResult.Withholding);
        var net = bonus - federalR - ssR - medicareR - addlR - stateR;

        var explanation = BuildExplanation(
            bonus, federalExplanation, ficaDetail, stateResult, net, input.State, input.TaxYear);
        var accuracyNotes = new List<AccuracyNote>
        {
            new("Exclusions", "Local taxes and state disability or paid-leave payroll assessments are not included in bonus mode.")
        };
        if (stateResult.UsesRegularMethod)
        {
            accuracyNotes.Add(new AccuracyNote(
                "Unsupported state method",
                $"{input.State} requires the regular/aggregate supplemental method. State withholding is omitted, so net bonus is before state income tax."));
        }

        return new BonusResult
        {
            BonusAmount = bonus,
            TaxYear = input.TaxYear,
            State = input.State,
            FederalWithholding = federalR,
            SocialSecurityWithholding = ssR,
            MedicareWithholding = medicareR,
            AdditionalMedicareWithholding = addlR,
            StateWithholding = stateR,
            StateMethod = stateResult.Method,
            StateUsesRegularMethod = stateResult.UsesRegularMethod,
            StateWithholdingDescription = stateResult.Description,
            NetBonus = RoundMoney(net),
            Explanation = explanation,
            AccuracyNotes = accuracyNotes
        };
    }

    private PaycheckExplanation BuildExplanation(
        decimal bonus,
        LineExplanation federalExplanation,
        FicaCalculationResult ficaDetail,
        StateSupplementalResult stateResult,
        decimal net,
        UsState state,
        int taxYear)
    {
        var lines = new List<LineExplanation>
        {
            new(ExplanationLineKey.GrossPay,
                "Bonus Amount",
                bonus,
                new List<ExplanationStep>
                {
                    new("Bonus / supplemental payment",
                        "The gross supplemental wage before federal, FICA, and state withholding.",
                        bonus,
                        $"= {Money(bonus)}"),
                }),
            AttachSources(federalExplanation, RuleIds("US", taxYear, TaxRuleScope.SupplementalWithholding)),
            AttachSources(ficaDetail.SocialSecurityExplanation, RuleIds("US", taxYear, TaxRuleScope.SocialSecurityMedicare)),
            AttachSources(ficaDetail.MedicareExplanation, RuleIds("US", taxYear, TaxRuleScope.SocialSecurityMedicare)),
        };

        if (ficaDetail.AdditionalMedicare > 0m)
            lines.Add(AttachSources(
                ficaDetail.AdditionalMedicareExplanation,
                RuleIds("US", taxYear, TaxRuleScope.AdditionalMedicare)));

        lines.Add(AttachSources(
            stateResult.Explanation,
            RuleIds(state, taxYear, TaxRuleScope.SupplementalWithholding)));
        lines.Add(BuildNetExplanation(bonus, federalExplanation.FinalAmount, ficaDetail, stateResult.Withholding, net));

        return new PaycheckExplanation(lines, _sourceCatalog);
    }

    private IReadOnlyList<string> RuleIds(UsState state, int taxYear, TaxRuleScope scope) =>
        _sourceCatalog?.RuleIds(state, taxYear, scope, TaxCalculationMode.Bonus)
        ?? Array.Empty<string>();

    private IReadOnlyList<string> RuleIds(string jurisdiction, int taxYear, TaxRuleScope scope) =>
        _sourceCatalog?.RuleIds(jurisdiction, taxYear, scope, TaxCalculationMode.Bonus)
        ?? Array.Empty<string>();

    private static LineExplanation AttachSources(
        LineExplanation line,
        IReadOnlyList<string> ruleIds) =>
        ruleIds.Count == 0 ? line : line with { SourceRuleIds = ruleIds };

    private static LineExplanation BuildNetExplanation(
        decimal bonus, decimal federal, FicaCalculationResult ficaDetail, decimal state, decimal net)
    {
        var totalTaxes = federal + ficaDetail.SocialSecurity + ficaDetail.Medicare + ficaDetail.AdditionalMedicare + state;
        var steps = new List<ExplanationStep>
        {
            new("Bonus amount", "Gross supplemental payment.", bonus, $"= {Money(bonus)}"),
            new("Less total withholding",
                "Federal supplemental, FICA (Social Security + Medicare + Additional Medicare), and state supplemental withholding.",
                totalTaxes,
                $"− {Money(totalTaxes)}"),
            new("Net bonus", "Take-home portion of the bonus.", net, $"= {Money(net)}"),
        };
        return new LineExplanation(ExplanationLineKey.NetPay, "Net Bonus", net, steps);
    }

    private static string Money(decimal v) => v.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

    private static decimal RoundMoney(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
