using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PaycheckCalc.Shared.Budgeting;
using PaycheckCalc.Shared.Json;
using PaycheckCalc.Shared.Sync;

namespace PaycheckCalc.Shared.Client;

/// <summary>
/// Typed client for the PaycheckCalc API: account register/login/logout against the ASP.NET Core
/// Identity bearer-token endpoints, and paycheck sync. Request URIs are built per call from
/// <see cref="IApiBaseAddressProvider"/> (never <c>HttpClient.BaseAddress</c>) so the server URL can
/// change at runtime. A 401 on sync triggers a single refresh-and-retry.
/// </summary>
public sealed class PaycheckApiClient
{
    private readonly HttpClient _http;
    private readonly IApiBaseAddressProvider _baseAddress;
    private readonly ITokenStore _tokens;

    public PaycheckApiClient(HttpClient http, IApiBaseAddressProvider baseAddress, ITokenStore tokens)
    {
        _http = http;
        _baseAddress = baseAddress;
        _tokens = tokens;
    }

    public async Task<ApiResult> RegisterAsync(string email, string password, CancellationToken ct = default)
    {
        var uri = BuildUri("api/account/register");
        if (uri is null) return ApiResult.Fail("No server URL is configured.");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonBody(new { email, password }) };
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode
                ? ApiResult.Ok()
                : ApiResult.Fail(await ReadErrorAsync(resp, ct).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ApiResult.Fail($"Could not reach the server: {ex.Message}");
        }
    }

    public async Task<ApiResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var uri = BuildUri("api/account/login");
        if (uri is null) return ApiResult.Fail("No server URL is configured.");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonBody(new { email, password }) };
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return ApiResult.Fail(resp.StatusCode == HttpStatusCode.Unauthorized
                    ? "Invalid email or password."
                    : await ReadErrorAsync(resp, ct).ConfigureAwait(false));

            var token = await ReadJsonAsync<AccessTokenResponse>(resp, ct).ConfigureAwait(false);
            if (token is null) return ApiResult.Fail("Unexpected login response from server.");
            await _tokens.SetTokensAsync(ToTokens(token), ct).ConfigureAwait(false);
            return ApiResult.Ok();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ApiResult.Fail($"Could not reach the server: {ex.Message}");
        }
    }

    /// <summary>Clears stored tokens. The Identity bearer scheme has no server-side logout.</summary>
    public Task LogoutAsync(CancellationToken ct = default)
        => _tokens.SetTokensAsync(null, ct).AsTask();

    public async Task<ApiResult<SyncResponse>> SyncAsync(SyncRequest request, CancellationToken ct = default)
    {
        var uri = BuildUri("api/paychecks/sync");
        if (uri is null) return ApiResult<SyncResponse>.Fail("No server URL is configured.");

        var tokens = await _tokens.GetTokensAsync(ct).ConfigureAwait(false);
        if (tokens is null) return ApiResult<SyncResponse>.Fail("Not signed in.");

        try
        {
            var resp = await PostSyncAsync(uri, request, tokens.AccessToken, ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                resp.Dispose();
                var refreshed = await TryRefreshAsync(tokens.RefreshToken, ct).ConfigureAwait(false);
                if (refreshed is null)
                    return ApiResult<SyncResponse>.Fail("Your session has expired. Please sign in again.");
                resp = await PostSyncAsync(uri, request, refreshed.AccessToken, ct).ConfigureAwait(false);
            }

            using (resp)
            {
                if (!resp.IsSuccessStatusCode)
                    return ApiResult<SyncResponse>.Fail(await ReadErrorAsync(resp, ct).ConfigureAwait(false));
                var body = await ReadJsonAsync<SyncResponse>(resp, ct).ConfigureAwait(false);
                return body is null
                    ? ApiResult<SyncResponse>.Fail("Unexpected sync response from server.")
                    : ApiResult<SyncResponse>.Ok(body);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ApiResult<SyncResponse>.Fail($"Could not reach the server: {ex.Message}");
        }
    }

    public async Task<ApiResult<BudgetSyncResponse>> SyncBudgetsAsync(BudgetSyncRequest request, CancellationToken ct = default)
    {
        var uri = BuildUri("api/budgets/sync");
        if (uri is null) return ApiResult<BudgetSyncResponse>.Fail("No server URL is configured.");

        var tokens = await _tokens.GetTokensAsync(ct).ConfigureAwait(false);
        if (tokens is null) return ApiResult<BudgetSyncResponse>.Fail("Not signed in.");

        try
        {
            var resp = await PostBudgetSyncAsync(uri, request, tokens.AccessToken, ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                resp.Dispose();
                var refreshed = await TryRefreshAsync(tokens.RefreshToken, ct).ConfigureAwait(false);
                if (refreshed is null)
                    return ApiResult<BudgetSyncResponse>.Fail("Your session has expired. Please sign in again.");
                resp = await PostBudgetSyncAsync(uri, request, refreshed.AccessToken, ct).ConfigureAwait(false);
            }

            using (resp)
            {
                if (!resp.IsSuccessStatusCode)
                    return ApiResult<BudgetSyncResponse>.Fail(await ReadErrorAsync(resp, ct).ConfigureAwait(false));
                var body = await ReadJsonAsync<BudgetSyncResponse>(resp, ct).ConfigureAwait(false);
                return body is null
                    ? ApiResult<BudgetSyncResponse>.Fail("Unexpected sync response from server.")
                    : ApiResult<BudgetSyncResponse>.Ok(body);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ApiResult<BudgetSyncResponse>.Fail($"Could not reach the server: {ex.Message}");
        }
    }

    private async Task<HttpResponseMessage> PostBudgetSyncAsync(Uri uri, BudgetSyncRequest request, string accessToken, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonBody(request) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _http.SendAsync(req, ct).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> PostSyncAsync(Uri uri, SyncRequest request, string accessToken, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonBody(request) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _http.SendAsync(req, ct).ConfigureAwait(false);
    }

    private async Task<AuthTokens?> TryRefreshAsync(string refreshToken, CancellationToken ct)
    {
        var uri = BuildUri("api/account/refresh");
        if (uri is null) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonBody(new { refreshToken }) };
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                await _tokens.SetTokensAsync(null, ct).ConfigureAwait(false);
                return null;
            }
            var token = await ReadJsonAsync<AccessTokenResponse>(resp, ct).ConfigureAwait(false);
            if (token is null) return null;
            var tokens = ToTokens(token);
            await _tokens.SetTokensAsync(tokens, ct).ConfigureAwait(false);
            return tokens;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    private Uri? BuildUri(string relative)
        => _baseAddress.BaseAddress is { } baseAddr ? new Uri(baseAddr, relative) : null;

    private static AuthTokens ToTokens(AccessTokenResponse token)
        => new(token.AccessToken, token.RefreshToken, DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn));

    private static StringContent JsonBody<T>(T value)
        => new(JsonSerializer.Serialize(value, PaycheckJson.Options), Encoding.UTF8, "application/json");

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, PaycheckJson.Options);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var problem = JsonSerializer.Deserialize<ValidationProblem>(json, PaycheckJson.Options);
                if (problem?.Errors is { Count: > 0 })
                    return string.Join(" ", problem.Errors.SelectMany(e => e.Value));
                if (!string.IsNullOrWhiteSpace(problem?.Title))
                    return problem!.Title!;
            }
        }
        catch (JsonException)
        {
            // Non-problem-details body; fall through to a generic message.
        }
        return $"Request failed ({(int)resp.StatusCode} {resp.ReasonPhrase}).";
    }

    // Shape of the Identity bearer-token endpoints' success body.
    private sealed record AccessTokenResponse(string TokenType, string AccessToken, int ExpiresIn, string RefreshToken);

    // Subset of RFC 7807 ValidationProblemDetails as returned by the Identity endpoints on 400.
    private sealed record ValidationProblem
    {
        public string? Title { get; init; }
        public Dictionary<string, string[]>? Errors { get; init; }
    }
}
