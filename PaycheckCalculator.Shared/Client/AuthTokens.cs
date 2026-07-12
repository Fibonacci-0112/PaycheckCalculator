namespace PaycheckCalculator.Shared.Client;

/// <summary>Bearer + refresh tokens issued by the Identity API, with the access token's expiry.</summary>
public sealed record AuthTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);
