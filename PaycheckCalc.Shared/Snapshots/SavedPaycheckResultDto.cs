namespace PaycheckCalc.Shared.Snapshots;

/// <summary>
/// The computed paycheck numbers captured in a saved snapshot. These mirror the flat numeric fields
/// the front-ends already display (the MAUI <c>ResultCardModel</c> / Blazor result panel), so a
/// restored snapshot renders without recomputation. The "Show Your Work" explanation is deliberately
/// not stored — it is regenerated on demand and would bloat the payload.
/// </summary>
public sealed record SavedPaycheckResultDto
{
    public decimal GrossPay { get; init; }
    public decimal FederalTaxableIncome { get; init; }
    public decimal FicaTaxableWages { get; init; }
    public decimal StateTaxableWages { get; init; }

    public decimal FederalWithholding { get; init; }
    public decimal SocialSecurityWithholding { get; init; }
    public decimal MedicareWithholding { get; init; }
    public decimal AdditionalMedicareWithholding { get; init; }
    public decimal StateWithholding { get; init; }
    public decimal StateDisabilityInsurance { get; init; }
    public string StateDisabilityInsuranceLabel { get; init; } = "State Disability Insurance";

    public decimal PreTaxDeductions { get; init; }
    public decimal PostTaxDeductions { get; init; }

    public decimal TotalTaxes { get; init; }
    public decimal NetPay { get; init; }

    /// <summary>True when this result came from the gross-up calculator.</summary>
    public bool IsGrossUp { get; init; }

    /// <summary>The desired net pay the gross-up targeted (0 when not a gross-up).</summary>
    public decimal TargetNetPay { get; init; }

    /// <summary>Extra gross beyond the target net that covers taxes and deductions (0 when not a gross-up).</summary>
    public decimal GrossUpCost { get; init; }
}
