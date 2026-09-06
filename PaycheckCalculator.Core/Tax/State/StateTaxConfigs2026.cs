using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// 2026 state income tax withholding configurations for states that use the
/// annualized percentage method.  Data sourced from halfpricesoft.com and
/// official state revenue department publications.
/// </summary>
public static class StateTaxConfigs2026
{
    public static IReadOnlyDictionary<UsState, PercentageMethodConfig> Configs { get; } =
        new Dictionary<UsState, PercentageMethodConfig>
        {
            // ── Flat-rate states ─────────────────────────────────────────

            // Arizona uses a dedicated calculator (ArizonaWithholdingCalculator)
            // — Form A-4 percentage-election method (0.5%–3.5% on gross
            // taxable wages, 2.0% default when no A-4 is on file).

            // Colorado uses a dedicated calculator (ColoradoWithholdingCalculator)

            // Georgia uses a dedicated calculator (GeorgiaWithholdingCalculator)
            // — flat 5.19% with G-4 filing statuses A/B/C/D, $12,000/$24,000
            // standard deductions, $4,000 dependent allowance, and $3,000
            // additional allowance per HB 111 and the 2026 Employer's Tax Guide.

            // Idaho uses a dedicated calculator (IdahoWithholdingCalculator)
            // — flat 5.3% (HB 521, 2024) with filing-status standard
            // deduction ($16,100 / $32,200) and $3,300 per ID W-4 allowance.

            // Illinois uses a dedicated calculator (IllinoisWithholdingCalculator)

            // Indiana uses a dedicated calculator (IndianaWithholdingCalculator)
            // — flat 3.05% with WH-4 personal/age/blind exemptions ($1,000 each)
            // and a separate additional dependent exemption ($3,000 each per
            // Indiana Departmental Notice #1, 2023+).

            // Iowa uses a dedicated calculator (IowaWithholdingCalculator)
            // — flat 3.65% with no standard deduction / allowance per the
            // Iowa Department of Revenue 2026 withholding formula.

            // Kentucky uses a dedicated calculator (KentuckyWithholdingCalculator)
            // — flat 4.0% with $3,160 standard deduction and $10 K-4 allowance
            // credit per the 2026 Form 42A003 withholding formula.

            // Massachusetts uses a dedicated calculator (MassachusettsWithholdingCalculator)
            // — M-4 filing statuses (Single/Married/Head of Household), personal
            // exemptions ($4,400/$8,800/$6,800), $1,000 per dependent, $2,200 per
            // blind exemption, $700 per age-65+ exemption, flat 5% rate with a 4%
            // surtax on annual taxable income above $1,000,000.

            // Michigan uses a dedicated calculator (MichiganWithholdingCalculator)
            // — flat 4.25% with MI-W4 exemptions at $5,900 per exemption per the
            // 2026 Form 446 Withholding Guide.

            // Mississippi uses a dedicated calculator (MississippiWithholdingCalculator)
            // — 89-350 filing statuses (Single/Married/Head of Household), filing-status
            // standard deduction ($2,300/$4,600/$3,400), filing-status personal exemption
            // ($6,000/$12,000/$9,500), $1,500 per dependent (Line 6), and two brackets
            // (0% on $0–$10,000, 4% over $10,000) per MS Pub 89-105 and HB 1 (2023).

            // North Carolina uses a dedicated calculator (NorthCarolinaWithholdingCalculator)
            // — NC-4 filing statuses (Single/Married/Head of Household),
            // $12,750/$25,500/$19,125 standard deduction, $2,500 per NC-4 allowance,
            // and a flat 4.5% rate per NC DOR Publication NC-30 (2026).

            // Utah uses a dedicated calculator (UtahWithholdingCalculator)
            // — federal W-4 filing statuses (Single/Married), flat 4.5% rate with a
            // phase-out allowance credit ($450/$900 per allowance for Single/Married,
            // phased out at 1.3% of wages above $9,107/$18,213) per Utah Publication 14 (2026).

            // ── Graduated-bracket states ─────────────────────────────────

            // Arkansas — handled by ArkansasWithholdingCalculator (DFA formula method)

            // California — handled by CaliforniaWithholdingCalculator (Method B)

            // Connecticut — handled by ConnecticutWithholdingCalculator (TPG-211 table-driven)

            // District of Columbia uses a dedicated calculator
            // (DistrictOfColumbiaWithholdingCalculator) — D-4 annualized
            // percentage method with filing-status standard deduction
            // ($15,000 / $30,000), $1,675 per-allowance exemption, and the
            // 2026 FR-230 graduated brackets (4%–10.75%).

            // Delaware uses a dedicated calculator (DelawareWithholdingCalculator)

            // Hawaii uses a dedicated calculator (HawaiiWithholdingCalculator)
            // — Booklet A percentage method with filing-status standard
            // deduction ($2,200 / $4,400), $1,144 per HW-4 allowance, and
            // the 2026 annual graduated brackets (1.4%–11.0%).

            // Kansas uses a dedicated calculator (KansasWithholdingCalculator)
            // — K-4 filing status (Single/Married), $3,605/$8,240 standard
            // deduction, $2,250 per K-4 allowance, and two brackets
            // (5.20% up to $23,000/$46,000, then 5.58%).

            // Louisiana uses a dedicated calculator (LouisianaWithholdingCalculator)
            // — L-4 filing statuses (Single/Married/Head of Household), $4,500/$9,000
            // personal exemption, $1,000 per-dependent deduction, and three graduated
            // brackets (1.85%/3.50%/4.25%) per Louisiana R-1306.

            // Maine uses a dedicated calculator (MaineWithholdingCalculator)
            // — W-4ME filing statuses (Single/Married), $15,300/$30,600 standard
            // deduction, $5,300 per W-4ME allowance, and three graduated brackets
            // (5.80%/6.75%/7.15%) per Maine Revenue Services 2026 Withholding Tables.

            // Maryland uses a dedicated calculator (MarylandWithholdingCalculator)
            // — MW507 filing statuses (Single/Married/Head of Household) and the
            // guide's per-payroll-period percentage method: a flat $3,400 standard
            // deduction and $3,200 per MW507 exemption, both prorated to the period,
            // a rate schedule floored at the 4.75% statutory minimum, and a separate
            // county income tax line, per the Comptroller of Maryland 2026 Employer
            // Withholding Guide. Note this one does not annualize.

            // Minnesota uses a dedicated calculator (MinnesotaWithholdingCalculator)
            // — W-4MN filing statuses (Single/Married/Head of Household),
            // $15,300/$30,600/$23,000 standard deduction, $5,300 per W-4MN allowance,
            // and four graduated brackets (5.35%/6.80%/7.85%/9.85%) per the Minnesota
            // Department of Revenue 2026 Withholding Tax Instructions and Tables (Pub. 89).

            // Missouri uses a dedicated calculator (MissouriWithholdingCalculator)
            // — MO W-4 filing statuses (Single/Married/Head of Household),
            // $15,750/$31,500/$23,625 standard deduction (mirrors federal),
            // $2,100 per MO W-4 allowance, and eight graduated brackets
            // (0%–4.7%) per the Missouri DOR 2026 Employer's Withholding Tax Guide.

            // Montana uses a dedicated calculator (MontanaWithholdingCalculator)
            // — MW-4 filing statuses (Single/Married/Head of Household), variable
            // standard deduction (20% of wages, min $4,370/$8,740, max $5,310/$10,620
            // for Single/Married), $3,040 per MW-4 exemption, and two brackets
            // (4.7% on $0–$23,800, 5.9% over $23,800) per the Montana DOR 2026
            // Withholding Tax Guide.

            // Nebraska uses a dedicated calculator (NebraskaWithholdingCalculator)
            // — W-4N filing statuses (Single/Married/Head of Household), standard
            // deductions $8,600/$17,200/$12,900, $171 per-allowance credit (applied
            // to computed tax), and four graduated brackets (2.46%/3.51%/5.01%/5.2%)
            // with filing-status–specific thresholds per the Nebraska DOR 2026 Circular EN.

            // New Jersey uses a dedicated calculator (NewJerseyWithholdingCalculator)
            // — NJ-W4 filing statuses A–E, $1,000 per-allowance deduction, no standard
            // deduction, Table A (single) brackets for Status A and C, and Table B
            // (married/HoH/surviving) brackets for Status B, D, and E, per the 2026
            // NJ-WT Employer's Guide.

            // New Mexico uses a dedicated calculator (NewMexicoWithholdingCalculator)
            // — RPD-41272 filing statuses (Single/Married/Head of Household),
            // $15,750/$31,500/$23,625 standard deduction (mirrors federal),
            // $4,000 per RPD-41272 exemption, and five graduated brackets
            // (1.7%/3.2%/4.7%/4.9%/5.9%) with filing-status–specific thresholds
            // per the NM Taxation and Revenue FYI-104 and NMSA §7-2-7 (SB 145, 2023).

            // New York uses a dedicated calculator (NewYorkWithholdingCalculator)
            // — IT-2104 filing statuses (Single/Married/Head of Household),
            // $8,000/$16,050/$11,000 standard deduction, $1,000 per IT-2104 allowance,
            // and ten graduated brackets (4%–10.9%) per NYS Publication NYS-50-T-NYS (2026).

            // North Dakota uses a dedicated calculator (NorthDakotaWithholdingCalculator)
            // — federal W-4 filing statuses (Single/Married/Head of Household), standard
            // deductions $15,750/$31,500/$23,625 (mirrors federal), and three graduated
            // brackets (1.10%/2.04%/2.64%) with filing-status–specific thresholds per the
            // ND Office of State Tax Commissioner 2026 Employer's Withholding Guide.

            // Ohio uses a dedicated calculator (OhioWithholdingCalculator)
            // — IT-4 exemption allowance ($650 annualized per exemption, no filing
            // status), and two brackets (0% on $0–$26,050, 2.75% over $26,050) per the
            // Ohio Department of Taxation 2026 Employer Withholding Tax – Optional
            // Computer Formula (HB 96, effective January 1, 2026).

            // Oregon uses a dedicated calculator (OregonWithholdingCalculator)
            // — OR-W-4 filing statuses (Single/Married/Head of Household),
            // $2,835/$5,670/$2,835 standard deduction (HoH uses Single deduction),
            // $219 per OR-W-4 allowance credit (applied to computed annual tax),
            // and four graduated brackets (4.75%/6.75%/8.75%/9.9%) where HoH uses
            // Married bracket thresholds per Oregon DOR Publication 150-206-436 (2026).

            // Rhode Island uses a dedicated calculator (RhodeIslandWithholdingCalculator)
            // — RI W-4 filing statuses (Single/Married/Head of Household), $10,550
            // standard deduction (same for all filing statuses), $4,700 per RI W-4
            // exemption, and three graduated brackets (3.75%/4.75%/5.99%) per the RI
            // Division of Taxation 2026 Pub. T-174.

            // South Carolina uses a dedicated calculator (SouthCarolinaWithholdingCalculator)
            // — SC W-4 filing statuses (Single/Married/Head of Household), a variable
            // standard deduction of 10% of annualized wages (maximum $7,500, only when
            // at least one allowance is claimed), $5,000 per SC W-4 allowance, and three
            // graduated brackets (0%/3%/6% at $0/$3,640/$18,230) per SCDOR Form WH-1603F.

            // Vermont uses a dedicated calculator (VermontWithholdingCalculator)
            // — W-4VT filing statuses (Single/Married/Head of Household), no state
            // standard deduction (allowances are the sole annualized offset), $5,400 per
            // W-4VT allowance, and four graduated brackets (3.35%/6.60%/7.60%/8.75%)
            // with filing-status–specific thresholds per the Vermont Department of Taxes
            // BP-55 (2026 Income Tax Withholding Instructions, Tables, and Charts).

            // Virginia uses a dedicated calculator (VirginiaWithholdingCalculator)
            // — VA-4 filing statuses (Single/Married/Head of Household),
            // $8,750 standard deduction for Single and $17,500 for Married/HoH,
            // $930 per VA-4 personal exemption, and four graduated brackets
            // (2%/3%/5%/5.75% at $0/$3,000/$5,000/$17,000) per the Virginia
            // Department of Taxation Employer Withholding Instructions (Pub. 93045, 2026).

            // West Virginia uses a dedicated calculator (WestVirginiaWithholdingCalculator)
            // — IT-104 filing statuses (Single/Married), no state standard deduction,
            // $2,000 per IT-104 personal exemption, and five graduated brackets
            // (3%/4%/4.5%/6%/6.5% at $0/$10,000/$25,000/$40,000/$60,000)
            // per the WV State Tax Dept. Form IT-104 and WV Code § 11-21-71 (2026).

            // Wisconsin uses a dedicated calculator (WisconsinWithholdingCalculator)
            // — WT-4 filing statuses (Single/Married/Head of Household), standard
            // deductions $12,760/$23,170/$16,840, $700 per WT-4 allowance, and four
            // graduated brackets (3.54%/4.65%/5.30%/7.65%) where Single and Head of
            // Household share bracket thresholds per WI DOR Publication W-166 (2026).
        };
}
