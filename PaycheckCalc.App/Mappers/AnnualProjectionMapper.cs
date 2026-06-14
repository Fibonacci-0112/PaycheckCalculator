using PaycheckCalc.App.Models;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps a domain <see cref="AnnualProjection"/> to an
/// <see cref="AnnualProjectionModel"/> presentation model for the
/// Results page's Annual sub-tab.
/// </summary>
public static class AnnualProjectionMapper
{
    public static AnnualProjectionModel Map(AnnualProjection p)
        => new()
        {
            PayPeriodsPerYear = p.PayPeriodsPerYear,
            CurrentPaycheckNumber = p.CurrentPaycheckNumber,
            RemainingPaychecks = p.RemainingPaychecks,

            AnnualizedGrossPay = p.AnnualizedGrossPay,
            AnnualizedPreTaxDeductions = p.AnnualizedPreTaxDeductions,
            AnnualizedFederalWithholding = p.AnnualizedFederalWithholding,
            AnnualizedStateWithholding = p.AnnualizedStateWithholding,
            AnnualizedFica = p.AnnualizedFica,
            AnnualizedNetPay = p.AnnualizedNetPay,

            ProjectedYtdGrossPay = p.ProjectedYtdGrossPay,
            ProjectedYtdFederalWithholding = p.ProjectedYtdFederalWithholding,
            ProjectedYtdStateWithholding = p.ProjectedYtdStateWithholding,
            ProjectedYtdFica = p.ProjectedYtdFica,
            ProjectedYtdNetPay = p.ProjectedYtdNetPay,

            EstimatedAnnualFederalLiability = p.EstimatedAnnualFederalLiability,
            EstimatedAnnualFicaLiability = p.EstimatedAnnualFicaLiability,
            EstimatedTotalLiability = p.EstimatedTotalLiability,
            AnnualizedTotalWithholding = p.AnnualizedTotalWithholding,
            OverUnderWithholding = p.OverUnderWithholding
        };
}
