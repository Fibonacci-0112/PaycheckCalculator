namespace PaycheckCalculator.Core.Tax.Federal;

public enum FederalFilingStatus
{
    SingleOrMarriedSeparately,
    MarriedFilingJointly,
    HeadOfHousehold
}

public sealed class FederalW4Input
{
    public FederalFilingStatus FilingStatus { get; init; } = FederalFilingStatus.SingleOrMarriedSeparately;

    // Form W-4 Step 2 checkbox
    public bool Step2Checked { get; init; } = false;

    // Annual amounts from W-4:
    public decimal Step3TaxCredits { get; init; } = 0m;     // Step 3
    public decimal Step4aOtherIncome { get; init; } = 0m;   // Step 4(a)
    public decimal Step4bDeductions { get; init; } = 0m;    // Step 4(b)

    // Per-pay-period amount from W-4:
    public decimal Step4cExtraWithholding { get; init; } = 0m; // Step 4(c)
}
