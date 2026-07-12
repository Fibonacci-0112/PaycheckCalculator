namespace PaycheckCalculator.Shared.Client;

/// <summary>
/// Stores the signed-in user's tokens. MAUI persists them in <c>SecureStorage</c>; Blazor keeps them
/// in circuit memory only (so they evaporate when the tab closes). <c>null</c> means signed out.
/// </summary>
public interface ITokenStore
{
    ValueTask<AuthTokens?> GetTokensAsync(CancellationToken ct = default);
    ValueTask SetTokensAsync(AuthTokens? tokens, CancellationToken ct = default);
}
