namespace PaycheckCalculator.Shared.Entitlements;

/// <summary>
/// Default implementation: everyone is on the free tier.
/// Swap this out when E2 billing is wired.
/// </summary>
public sealed class FreeEntitlementProvider : IEntitlementProvider
{
    public bool IsPro => false;
}
