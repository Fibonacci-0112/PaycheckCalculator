namespace PaycheckCalculator.Shared.Entitlements;

/// <summary>
/// Reports whether the current user has an active Pro subscription.
/// Implementations vary by platform: MAUI checks a store receipt/preference;
/// Blazor checks the circuit account session. See E2 for billing integration.
/// </summary>
public interface IEntitlementProvider
{
    bool IsPro { get; }
}
