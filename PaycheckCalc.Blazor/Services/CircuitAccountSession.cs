using PaycheckCalc.Shared.Client;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// Holds the signed-in user's tokens and email in circuit memory only. Scoped to the Blazor Server
/// circuit, so credentials never outlive the browser tab and are never written to disk.
/// </summary>
public sealed class CircuitAccountSession : ITokenStore
{
    private AuthTokens? _tokens;

    /// <summary>The signed-in user's email, for display. <c>null</c> when signed out.</summary>
    public string? Email { get; set; }

    public bool IsSignedIn => _tokens is not null;

    public ValueTask<AuthTokens?> GetTokensAsync(CancellationToken ct = default) => new(_tokens);

    public ValueTask SetTokensAsync(AuthTokens? tokens, CancellationToken ct = default)
    {
        _tokens = tokens;
        if (tokens is null) Email = null;
        return ValueTask.CompletedTask;
    }
}
