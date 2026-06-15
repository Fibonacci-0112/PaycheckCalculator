using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Blazor.Services;

public record StateInfo(
    UsState State,
    string FullName,
    string Slug,
    string Title,
    string MetaDescription,
    string WithholdingBlurb
);

/// <summary>
/// SEO and descriptive metadata for every supported state landing page.
/// Keyed by URL slug (e.g. "california") and UsState enum value.
/// </summary>
public static class StateMetadata
{
    private static readonly Dictionary<string, StateInfo> _bySlug;
    private static readonly Dictionary<UsState, StateInfo> _byState;

    static StateMetadata()
    {
        var all = BuildAll();
        _bySlug  = all.ToDictionary(s => s.Slug, StringComparer.OrdinalIgnoreCase);
        _byState = all.ToDictionary(s => s.State);
    }

    public static StateInfo? GetBySlug(string slug) =>
        _bySlug.TryGetValue(slug, out var info) ? info : null;

    public static StateInfo? GetByState(UsState state) =>
        _byState.TryGetValue(state, out var info) ? info : null;

    public static IReadOnlyCollection<StateInfo> All => _bySlug.Values;

    private static List<StateInfo> BuildAll() => new()
    {
        S(UsState.AK, "Alaska", "alaska",
            "Alaska Paycheck Calculator 2026 — Free Take-Home Pay Estimator",
            "Calculate your Alaska take-home pay for 2026. Alaska has no state income tax — estimate your federal withholding, Social Security, and Medicare in seconds.",
            "Alaska has no state income tax, so there is no state withholding on Alaska paychecks. " +
            "Your gross pay is reduced only by federal income tax (computed per IRS Publication 15-T 2026), " +
            "Social Security (6.2% up to the $184,500 wage base), Medicare (1.45%), " +
            "and any pre- or post-tax deductions you elect."),

        S(UsState.AL, "Alabama", "alabama",
            "Alabama Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Alabama take-home pay for 2026. Accurate Alabama state income tax withholding — progressive brackets, dependent deductions, and more.",
            "Alabama uses a progressive withholding method with brackets of 2%, 4%, and 5%. " +
            "Standard deductions ($2,500 for single, $7,500 for married) and dependent deductions ($1,000 per dependent) " +
            "reduce Alabama taxable wages. Uniquely, Alabama's withholding computation also references your " +
            "annualized federal income tax, making accurate computation require both federal and state inputs together."),

        S(UsState.AR, "Arkansas", "arkansas",
            "Arkansas Paycheck Calculator 2026 — State Income Tax & Take-Home Pay",
            "Calculate your Arkansas take-home pay for 2026. Arkansas uses graduated brackets from 2% to 4.4% via the annualized withholding method.",
            "Arkansas uses graduated income tax brackets ranging from 2% on lower income up to 4.4% " +
            "on income exceeding approximately $89,600 (annualized). Withholding is computed by annualizing " +
            "the period wages, applying the bracket schedule, then de-annualizing back to the pay period. " +
            "A low-income exemption reduces or eliminates withholding for earners below a threshold."),

        S(UsState.AZ, "Arizona", "arizona",
            "Arizona Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Arizona take-home pay for 2026. Arizona uses a flat 2.5% income tax rate — one of the lowest in the nation.",
            "Arizona uses a flat 2.5% state income tax rate for 2026 — one of the lowest flat rates among states " +
            "with an income tax. Withholding is computed by annualizing gross wages, subtracting the standard " +
            "withholding allowance claimed on your A-4, applying 2.5%, then de-annualizing to the pay period."),

        S(UsState.CA, "California", "california",
            "California Paycheck Calculator 2026 — State Tax, SDI & Take-Home Pay",
            "Calculate your California take-home pay for 2026. Accurate CA state income tax via Method B (up to 13.3%) plus SDI computed instantly.",
            "California uses the income tax Method B (percentage method) with 10 progressive tax brackets " +
            "ranging up to 13.3% for the highest earners — the highest top marginal rate in the US. " +
            "State Disability Insurance (SDI) is also withheld at 1.1% of gross wages with no wage-base cap for 2026. " +
            "Your DE-4 allowance certificate determines your standard deduction and withholding exemptions."),

        S(UsState.CO, "Colorado", "colorado",
            "Colorado Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Colorado take-home pay for 2026. Colorado uses a flat 4.4% income tax rate with DR 0004 allowances.",
            "Colorado uses a flat 4.4% state income tax rate for 2026. The DR 0004 employee withholding " +
            "certificate allows you to claim standard deduction allowances that reduce Colorado taxable wages. " +
            "Withholding is simply Colorado taxable wages multiplied by 4.4%, then de-annualized to the pay period."),

        S(UsState.CT, "Connecticut", "connecticut",
            "Connecticut Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Connecticut take-home pay for 2026. Connecticut uses the TPG-211 percentage tables with five brackets up to 6.99%.",
            "Connecticut uses the TPG-211 percentage withholding tables with five progressive brackets " +
            "ranging from 3.0% up to 6.99%. The system includes a complex phase-out structure that reduces " +
            "the benefit of lower brackets as income increases. Your filing status (filing status codes A, B, C, or D " +
            "from your CT-W4) determines which withholding table applies."),

        S(UsState.DC, "District of Columbia", "district-of-columbia",
            "D.C. Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Washington D.C. take-home pay for 2026. D.C. uses progressive income tax brackets from 4% up to 10.75%.",
            "Washington D.C. uses a progressive income tax with brackets ranging from 4% on lower income " +
            "up to 10.75% on income above approximately $1 million (annualized). " +
            "Withholding is computed using the annualized method with standard deduction amounts " +
            "and personal exemption credits determined by your D-4 filing status."),

        S(UsState.DE, "Delaware", "delaware",
            "Delaware Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Delaware take-home pay for 2026. Delaware uses progressive brackets from 0% up to 6.6%.",
            "Delaware uses a progressive income tax with six brackets from 0% on income under $2,000 " +
            "up to 6.6% on income above $60,000 (annualized). " +
            "A standard deduction and personal credit reduce Delaware taxable income. " +
            "Withholding is computed using the annualized method."),

        S(UsState.FL, "Florida", "florida",
            "Florida Paycheck Calculator 2026 — Free Take-Home Pay Estimator",
            "Calculate your Florida take-home pay for 2026. Florida has no state income tax — only federal taxes and FICA apply.",
            "Florida has no state income tax, so there is no state withholding deducted from Florida paychecks. " +
            "Your take-home pay is reduced only by federal income tax, Social Security (6.2%), " +
            "Medicare (1.45%), and any elected pre- or post-tax deductions. " +
            "Florida is one of nine states with no state income tax."),

        S(UsState.GA, "Georgia", "georgia",
            "Georgia Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Georgia take-home pay for 2026. Georgia uses a flat income tax rate that decreases each year toward 4.99%.",
            "Georgia transitioned to a flat state income tax rate starting in 2024 and the rate decreases " +
            "each year as part of Georgia's multi-year tax reform. " +
            "Standard deductions and personal exemption amounts reduce Georgia taxable wages. " +
            "Withholding is computed on the annualized period wages after deductions, then de-annualized."),

        S(UsState.HI, "Hawaii", "hawaii",
            "Hawaii Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Hawaii take-home pay for 2026. Hawaii has one of the most progressive tax structures with up to 11% on high incomes.",
            "Hawaii has one of the most progressive state income tax structures in the US, " +
            "with nine brackets ranging from 1.4% on the lowest income up to 11% on income above $400,000 " +
            "(annualized for single filers). " +
            "Personal exemption credits by filing status reduce the final withholding amount. " +
            "Withholding uses the annualized computation method."),

        S(UsState.IA, "Iowa", "iowa",
            "Iowa Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Iowa take-home pay for 2026. Iowa transitioned to a flat income tax rate for 2026 under Iowa's multi-year tax reform.",
            "Iowa moved to a flat state income tax rate for 2026 as part of Iowa's multi-year tax reform legislation, " +
            "replacing the prior graduated bracket structure. " +
            "Standard deductions and personal credits reduce Iowa taxable wages. " +
            "Withholding uses the annualized method."),

        S(UsState.ID, "Idaho", "idaho",
            "Idaho Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Idaho take-home pay for 2026. Idaho uses a flat 5.695% income tax rate.",
            "Idaho uses a flat 5.695% state income tax rate for 2026. " +
            "Standard deduction and personal exemption amounts by filing status reduce Idaho taxable wages. " +
            "Withholding is computed by annualizing wages, subtracting allowances, applying 5.695%, " +
            "then de-annualizing to the pay period."),

        S(UsState.IL, "Illinois", "illinois",
            "Illinois Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Illinois take-home pay for 2026. Illinois uses a flat 4.95% income tax with no deductions for most filers.",
            "Illinois uses a flat 4.95% state income tax rate. " +
            "Illinois taxable income is gross wages minus any exempt allowances claimed on your IL-W-4. " +
            "No graduated brackets apply — the flat rate makes Illinois withholding one of the most " +
            "straightforward computations among states with an income tax."),

        S(UsState.IN, "Indiana", "indiana",
            "Indiana Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Indiana take-home pay for 2026. Indiana uses a flat state income tax rate plus county income tax for most residents.",
            "Indiana uses a flat state income tax rate of approximately 3.05% for 2026 as part of Indiana's " +
            "gradual rate reduction schedule. Additionally, most Indiana counties levy a county income tax " +
            "(ranging from about 0.5% to 3.38%) that is also withheld from paychecks based on your county " +
            "of residence or principal place of work."),

        S(UsState.KS, "Kansas", "kansas",
            "Kansas Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Kansas take-home pay for 2026. Kansas uses three income tax brackets from 3.1% to 5.7%.",
            "Kansas uses a progressive income tax with three brackets: 3.1% on the first $15,000, " +
            "5.25% on income from $15,001 to $30,000, and 5.7% on income above $30,000 (annualized for single filers; " +
            "higher thresholds for married filing jointly). " +
            "Standard deductions and personal exemption credits reduce Kansas taxable income."),

        S(UsState.KY, "Kentucky", "kentucky",
            "Kentucky Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Kentucky take-home pay for 2026. Kentucky uses a flat 4.0% income tax rate.",
            "Kentucky uses a flat 4.0% state income tax rate for 2026. " +
            "Standard allowances reduce Kentucky taxable wages. " +
            "Kentucky's flat rate replaced a prior graduated bracket structure as part of the Commonwealth's " +
            "ongoing tax modernization, with the rate scheduled to continue declining subject to revenue conditions."),

        S(UsState.LA, "Louisiana", "louisiana",
            "Louisiana Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Louisiana take-home pay for 2026. Louisiana uses three progressive brackets from 1.85% up to 4.25%.",
            "Louisiana uses a progressive income tax with three brackets: 1.85% on the first $12,500 of " +
            "annualized income, 3.5% on the next $37,500, and 4.25% on amounts above $50,000. " +
            "Dependent exemption credits (rather than deductions) directly reduce the withholding amount. " +
            "Withholding uses the annualized method."),

        S(UsState.MA, "Massachusetts", "massachusetts",
            "Massachusetts Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Massachusetts take-home pay for 2026. Massachusetts uses a flat 5% income tax rate on most wages.",
            "Massachusetts uses a flat 5.0% state income tax rate on most wages and salary income. " +
            "A personal exemption credit reduces withholding based on your M-4 filing status. " +
            "Note: Massachusetts also imposes a 4% surtax on annual income over $1 million at filing time — " +
            "this additional tax is not factored into standard per-paycheck withholding."),

        S(UsState.MD, "Maryland", "maryland",
            "Maryland Paycheck Calculator 2026 — State + County Tax & Take-Home Pay",
            "Calculate your Maryland take-home pay for 2026. Maryland combines progressive state income tax (2%–5.75%) with mandatory county withholding.",
            "Maryland withholding combines a progressive state income tax (six brackets from 2% up to 5.75%) " +
            "with mandatory county income tax withholding. County rates vary by jurisdiction — " +
            "ranging from about 2.25% to 3.2% — and are withheld from every paycheck based on your county " +
            "of residence. Total Maryland effective withholding is typically 6–9% of wages depending on income and county."),

        S(UsState.ME, "Maine", "maine",
            "Maine Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Maine take-home pay for 2026. Maine uses three progressive income tax brackets from 5.8% up to 7.15%.",
            "Maine uses a progressive income tax with three brackets: 5.8% on income up to approximately $24,500, " +
            "6.75% on income from $24,500 to $58,050, and 7.15% on income above $58,050 " +
            "(annualized, thresholds vary by filing status). " +
            "Standard deduction and personal credit amounts reduce taxable income. " +
            "Withholding uses the annualized method."),

        S(UsState.MI, "Michigan", "michigan",
            "Michigan Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Michigan take-home pay for 2026. Michigan uses a flat income tax rate that adjusts annually based on revenue triggers.",
            "Michigan uses a flat state income tax rate for 2026; the rate adjusts each year based on " +
            "Michigan's General Fund revenue triggers. " +
            "Michigan taxable wages are reduced by personal exemption amounts ($5,400 per exemption). " +
            "Some Michigan cities also levy their own income tax of 1%–2.4% withheld separately."),

        S(UsState.MN, "Minnesota", "minnesota",
            "Minnesota Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Minnesota take-home pay for 2026. Minnesota uses four progressive brackets from 5.35% to 9.85%.",
            "Minnesota uses a progressive income tax with four brackets: 5.35%, 6.80%, 7.85%, and 9.85%. " +
            "Minnesota has relatively high effective rates for mid-to-high earners. " +
            "Standard deduction amounts and personal exemption credits by filing status reduce taxable income. " +
            "Withholding uses the annualized method per Minnesota's withholding tables."),

        S(UsState.MO, "Missouri", "missouri",
            "Missouri Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Missouri take-home pay for 2026. Missouri uses progressive brackets topping at 4.95%.",
            "Missouri uses a progressive income tax with multiple brackets topping at 4.95% on income " +
            "above $9,000 (annualized). Missouri uniquely allows a deduction for a portion of " +
            "federal income tax paid, which reduces Missouri taxable income. " +
            "Standard deductions and personal credits also apply. Withholding uses the annualized method."),

        S(UsState.MS, "Mississippi", "mississippi",
            "Mississippi Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Mississippi take-home pay for 2026. Mississippi uses a flat 5% income tax rate for 2026.",
            "Mississippi uses a flat 5.0% state income tax rate for 2026, having replaced its prior " +
            "graduated bracket structure. Standard deductions and personal exemptions reduce " +
            "Mississippi taxable wages. Withholding uses the annualized method with the flat rate applied " +
            "after allowances are deducted."),

        S(UsState.MT, "Montana", "montana",
            "Montana Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Montana take-home pay for 2026. Montana uses a flat 5.9% income tax rate, replacing its prior graduated brackets.",
            "Montana adopted a flat 5.9% state income tax rate effective from 2024, replacing its prior " +
            "graduated bracket system. Standard deductions and personal exemption amounts reduce Montana taxable wages. " +
            "Withholding is computed by annualizing wages, subtracting allowances, applying 5.9%, " +
            "then de-annualizing to the pay period."),

        S(UsState.NC, "North Carolina", "north-carolina",
            "North Carolina Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your North Carolina take-home pay for 2026. North Carolina uses a flat income tax rate that continues decreasing each year.",
            "North Carolina uses a flat state income tax rate of 4.5% for 2026 as part of the state's " +
            "multi-year reduction schedule. The rate has been declining since 2022 and is scheduled to " +
            "continue decreasing. Standard deduction amounts reduce North Carolina taxable wages. " +
            "Withholding uses the annualized method."),

        S(UsState.ND, "North Dakota", "north-dakota",
            "North Dakota Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your North Dakota take-home pay for 2026. North Dakota uses very low progressive rates from 1.1% to 2.27%.",
            "North Dakota uses a progressive income tax with very low rates — one of the most favorable " +
            "income tax structures among states with an income tax: 1.1% on income up to approximately " +
            "$44,725, 2.04% on income from $44,726 to $225,975, and 2.27% on income above $225,975 " +
            "(annualized, thresholds vary by filing status). Standard deductions and personal exemptions apply."),

        S(UsState.NE, "Nebraska", "nebraska",
            "Nebraska Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Nebraska take-home pay for 2026. Nebraska uses four progressive income tax brackets up to 6.84%.",
            "Nebraska uses a progressive income tax with four brackets ranging from 2.46% on the lowest " +
            "income up to 6.84% on income above approximately $33,180 (annualized for single filers; " +
            "higher thresholds for joint filers). " +
            "Standard deduction and personal credit amounts by filing status reduce taxable income."),

        S(UsState.NH, "New Hampshire", "new-hampshire",
            "New Hampshire Paycheck Calculator 2026 — Free Take-Home Pay Estimator",
            "Calculate your New Hampshire take-home pay for 2026. New Hampshire has no tax on earned wages — only federal taxes and FICA apply.",
            "New Hampshire does not tax earned wages or salary income, so there is no state income tax " +
            "withholding on New Hampshire paychecks. " +
            "Only federal income tax, Social Security, Medicare, and any elected deductions reduce your take-home pay. " +
            "(New Hampshire's tax on interest and dividends is handled at annual filing, not through payroll withholding.)"),

        S(UsState.NJ, "New Jersey", "new-jersey",
            "New Jersey Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your New Jersey take-home pay for 2026. New Jersey uses seven progressive brackets up to 10.75% — one of the highest top rates.",
            "New Jersey uses a progressive income tax with seven brackets from 1.4% on the lowest income " +
            "up to 10.75% on income above $5 million (annualized). " +
            "New Jersey's withholding tables (NJ-WT) differ from the standard annualized method; " +
            "rates apply in incremental layers, and the correct bracket selection depends on pay frequency and filing status."),

        S(UsState.NM, "New Mexico", "new-mexico",
            "New Mexico Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your New Mexico take-home pay for 2026. New Mexico uses five progressive income tax brackets up to 5.9%.",
            "New Mexico uses a progressive income tax with five brackets ranging from 1.7% on lower income " +
            "up to 5.9% on income above approximately $210,000 (annualized for single filers). " +
            "Personal exemption credits by filing status reduce the New Mexico withholding amount. " +
            "Withholding uses the annualized method."),

        S(UsState.NV, "Nevada", "nevada",
            "Nevada Paycheck Calculator 2026 — Free Take-Home Pay Estimator",
            "Calculate your Nevada take-home pay for 2026. Nevada has no state income tax — only federal taxes and FICA are withheld.",
            "Nevada is one of nine states with no state income tax, so there is no state withholding on " +
            "Nevada paychecks. Your gross pay is reduced only by federal income tax, Social Security (6.2%), " +
            "Medicare (1.45%), and any elected deductions. " +
            "Nevada workers generally enjoy higher take-home pay compared to most states."),

        S(UsState.NY, "New York", "new-york",
            "New York Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your New York take-home pay for 2026. New York uses progressive brackets up to 10.9%, plus NYC income tax for city residents.",
            "New York uses a progressive income tax with eight brackets from 4% up to 10.9% on income " +
            "above $25 million (annualized). New York City residents additionally pay NYC income tax " +
            "of 3.078% to 3.876% withheld through the same payroll system. " +
            "Standard deduction amounts and dependent credits reduce taxable income. " +
            "New York withholding uses the annualized percentage method."),

        S(UsState.OH, "Ohio", "ohio",
            "Ohio Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Ohio take-home pay for 2026. Ohio uses low progressive income tax brackets from 2.75% to 3.99%.",
            "Ohio uses a progressive income tax with brackets ranging from 2.75% on lower income up to " +
            "3.99% on income above approximately $110,650 (annualized). " +
            "Ohio also provides a low-income exemption for earners below the threshold. " +
            "Additionally, many Ohio school districts levy a separate school district income tax withheld from paychecks."),

        S(UsState.OK, "Oklahoma", "oklahoma",
            "Oklahoma Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Oklahoma take-home pay for 2026. Oklahoma uses the OW-2 withholding tables with five brackets up to 4.75%.",
            "Oklahoma uses a progressive income tax computed from the OW-2 withholding tables with five brackets " +
            "from 0.25% up to 4.75%. Oklahoma has a legally-required withholding computation that rounds the " +
            "per-period withholding to the nearest whole dollar — a unique requirement among US states. " +
            "Standard deductions and personal exemptions reduce Oklahoma taxable income."),

        S(UsState.OR, "Oregon", "oregon",
            "Oregon Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Oregon take-home pay for 2026. Oregon has one of the highest top rates at 11%, plus the Statewide Transit Tax.",
            "Oregon has one of the highest state income tax top rates in the US, with four brackets from " +
            "4.75% up to 11% on income above $250,000 (annualized for single filers). " +
            "The Oregon Statewide Transit Tax (0.1%) is also withheld separately. " +
            "Standard deductions and personal exemption credits reduce Oregon taxable income. " +
            "Withholding uses the annualized method."),

        S(UsState.PA, "Pennsylvania", "pennsylvania",
            "Pennsylvania Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Pennsylvania take-home pay for 2026. Pennsylvania uses a simple flat 3.07% income tax with no deductions.",
            "Pennsylvania uses a flat 3.07% state income tax rate — one of the simplest withholding " +
            "computations in the US. No standard deductions or graduated brackets apply for most employees. " +
            "Local Earned Income Tax (EIT) may also be withheld depending on your municipality and workplace " +
            "location, typically 1%–3.07%."),

        S(UsState.RI, "Rhode Island", "rhode-island",
            "Rhode Island Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Rhode Island take-home pay for 2026. Rhode Island uses three progressive brackets from 3.75% to 5.99%.",
            "Rhode Island uses a progressive income tax with three brackets: 3.75% on income up to " +
            "approximately $77,450, 4.75% on income from $77,451 to $176,050, and 5.99% on income above " +
            "$176,050 (annualized, thresholds vary by year). " +
            "Standard deduction and personal exemption credits reduce taxable income. Withholding uses the annualized method."),

        S(UsState.SC, "South Carolina", "south-carolina",
            "South Carolina Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your South Carolina take-home pay for 2026. South Carolina uses progressive brackets up to 7%, reducing each year.",
            "South Carolina uses a progressive income tax with six brackets from 0% up to 7% on higher incomes " +
            "(annualized). South Carolina is phasing its top marginal rate downward over multiple years. " +
            "Standard deduction and personal exemption amounts vary by filing status. " +
            "Withholding uses the annualized method."),

        S(UsState.SD, "South Dakota", "south-dakota",
            "South Dakota Paycheck Calculator 2026 — Free Take-Home Pay Estimator",
            "Calculate your South Dakota take-home pay for 2026. South Dakota has no state income tax — only federal taxes and FICA are withheld.",
            "South Dakota has no state income tax and no state payroll withholding for income tax purposes. " +
            "Your net pay is reduced only by federal income tax, Social Security (6.2% up to $184,500), " +
            "Medicare (1.45%), and any elected pre- or post-tax deductions. " +
            "South Dakota is one of nine states with no state income tax."),

        S(UsState.TN, "Tennessee", "tennessee",
            "Tennessee Paycheck Calculator 2026 — Free Take-Home Pay Estimator",
            "Calculate your Tennessee take-home pay for 2026. Tennessee has no state income tax on wages — only federal taxes and FICA apply.",
            "Tennessee has no state income tax on earned wages or salary. The Hall Income Tax on " +
            "interest and dividends was fully repealed in 2021. " +
            "Tennessee paychecks are reduced only by federal income tax, FICA taxes, and any elected deductions. " +
            "Tennessee is one of nine states with no state income tax."),

        S(UsState.TX, "Texas", "texas",
            "Texas Paycheck Calculator 2026 — Free Take-Home Pay Estimator",
            "Calculate your Texas take-home pay for 2026. Texas has no state income tax — one of the highest take-home pay states in the US.",
            "Texas has no state income tax, making it one of the most favorable states for take-home pay. " +
            "Texas paychecks are reduced only by federal income tax (IRS Pub 15-T 2026), " +
            "Social Security (6.2% up to the $184,500 wage base), Medicare (1.45%), " +
            "and any elected deductions. No state withholding form is required for Texas employees."),

        S(UsState.UT, "Utah", "utah",
            "Utah Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Utah take-home pay for 2026. Utah uses a flat 4.55% income tax rate with a personal exemption credit.",
            "Utah uses a flat 4.55% state income tax rate for 2026. " +
            "A non-refundable personal exemption credit (based on your TC-40W filing status) directly " +
            "reduces the final withholding amount. " +
            "Withholding is computed by annualizing wages, computing tentative tax at 4.55%, " +
            "subtracting the exemption credit, then de-annualizing."),

        S(UsState.VA, "Virginia", "virginia",
            "Virginia Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Virginia take-home pay for 2026. Virginia uses four progressive income tax brackets up to 5.75%.",
            "Virginia uses a progressive income tax with four brackets: 2% on income up to $3,000, " +
            "3% on $3,001–$5,000, 5% on $5,001–$17,000, and 5.75% on income above $17,000 (annualized). " +
            "Standard deductions ($8,000 single / $16,000 married) and personal exemption amounts from " +
            "your VA-4 reduce Virginia taxable income. Withholding uses the annualized method."),

        S(UsState.VT, "Vermont", "vermont",
            "Vermont Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Vermont take-home pay for 2026. Vermont uses four progressive brackets from 3.35% to 8.75%.",
            "Vermont uses a progressive income tax with four brackets from 3.35% on lower income up to " +
            "8.75% on income above approximately $213,150 (annualized for single filers). " +
            "Vermont withholding is computed as a percentage of federal withholding (a simplified approach), " +
            "adjusted for Vermont exemptions claimed on your W-4VT."),

        S(UsState.WA, "Washington", "washington",
            "Washington Paycheck Calculator 2026 — WA Cares Fund & Take-Home Pay",
            "Calculate your Washington State take-home pay for 2026. Washington has no income tax but withholds 0.58% for the WA Cares Fund long-term care program.",
            "Washington State has no traditional income tax on wages, so there is no state income tax withholding. " +
            "However, Washington does withhold for the WA Cares Fund long-term care insurance program at " +
            "0.58% of gross wages (no wage-base cap). " +
            "Employees who hold an approved private long-term care insurance policy may apply to opt out " +
            "of WA Cares withholding."),

        S(UsState.WI, "Wisconsin", "wisconsin",
            "Wisconsin Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your Wisconsin take-home pay for 2026. Wisconsin uses four progressive income tax brackets from 3.50% to 7.65%.",
            "Wisconsin uses a progressive income tax with four brackets: 3.50%, 4.40%, 5.30%, and 7.65% " +
            "on income above approximately $315,310 (annualized for single filers; higher threshold for joint). " +
            "Standard deduction and personal exemption amounts vary by filing status. " +
            "Withholding uses the annualized percentage method per Wisconsin's WT-4 tables."),

        S(UsState.WV, "West Virginia", "west-virginia",
            "West Virginia Paycheck Calculator 2026 — State Tax & Take-Home Pay",
            "Calculate your West Virginia take-home pay for 2026. West Virginia uses five progressive brackets from 3% to 6.5%.",
            "West Virginia uses a progressive income tax with five brackets from 3% on lower income up to " +
            "6.5% on income above $60,000 (annualized). " +
            "Personal exemption credits by filing status and number of dependents reduce West Virginia withholding. " +
            "Withholding uses the annualized percentage method."),

        S(UsState.WY, "Wyoming", "wyoming",
            "Wyoming Paycheck Calculator 2026 — Free Take-Home Pay Estimator",
            "Calculate your Wyoming take-home pay for 2026. Wyoming has no state income tax — only federal taxes and FICA are withheld.",
            "Wyoming has no state income tax, making it one of the most tax-friendly states for employees. " +
            "Wyoming paychecks are reduced only by federal income tax, Social Security (6.2%), " +
            "Medicare (1.45%), and any elected deductions. " +
            "Wyoming is one of nine states with no state income tax."),
    };

    private static StateInfo S(UsState state, string fullName, string slug,
        string title, string metaDesc, string blurb)
        => new(state, fullName, slug, title, metaDesc, blurb);
}
