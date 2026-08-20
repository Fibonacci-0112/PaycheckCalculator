# 10 — Shared Contracts & Sync

`PaycheckCalculator.Shared` is the portable layer that lets saved paychecks (and budgets — see
[08 — Budgeting](08-budgeting.md)) sync across the MAUI app, the Blazor app, and the API server
without any of them depending on each other. It references Core only.

Sync is **entirely optional**. The MAUI app persists saved paychecks on-device with no account
required — it works fully offline. The Blazor app keeps anonymous saved paychecks in
circuit-scoped memory only, for the lifetime of the browser tab. Signing in adds cross-device
sync on top of whichever local behavior already exists; it does not replace it.

---

## Saved-paycheck snapshot model (`Shared/Snapshots/`)

### `SavedPaycheckDto`

```csharp
public sealed record SavedPaycheckDto
{
    public required string Name { get; init; }             // sync key, case-insensitive
    public DateTimeOffset UpdatedAtUtc { get; init; }       // drives last-write-wins
    public int SchemaVersion { get; init; } = 1;
    public required PaycheckInput Input { get; init; }      // the exact input that produced this
    public required SavedPaycheckResultDto Result { get; init; }
}
```

The full `PaycheckInput` is stored — so a saved paycheck can, in principle, be reloaded into the
calculator and recalculated — alongside a **flattened** result snapshot.

### `SavedPaycheckResultDto` and `SavedPaycheckResultMapper`

```csharp
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
    public bool IsGrossUp { get; init; }
    public decimal TargetNetPay { get; init; }
    public decimal GrossUpCost { get; init; }
    public int TaxYear { get; init; } = TaxYearSupport.Default;
}
```

Mirrors the flat numeric fields both front-ends already display, so a restored snapshot renders
**without recomputation** — important for a snapshot from a prior tax year, which should show
exactly what it showed when calculated, not what the current engine would produce for those
inputs.

**Deliberately absent: the `PaycheckExplanation`.** Storing the full "Show Your Work" tree with
every saved paycheck would bloat every sync payload for a feature (the explanation) that is cheap
to regenerate on demand from the stored `Input`. `SavedPaycheckResultMapper.FromResult` is the one
place a domain `PaycheckResult` becomes this flattened shape, used identically by both front-ends
so the mapping never drifts.

### Tombstones and the full set

```csharp
public sealed record SavedPaycheckTombstone(string Name, DateTimeOffset DeletedAtUtc);

public sealed record SavedPaycheckSet(
    IReadOnlyList<SavedPaycheckDto> Paychecks,
    IReadOnlyList<SavedPaycheckTombstone> Tombstones)
{
    public static SavedPaycheckSet Empty { get; } = new([], []);
}
```

A delete does not simply remove an entry — it **replaces** it with a tombstone recording when the
deletion happened. Tombstones are kept even for anonymous (never-synced) local data, so that if
the same paycheck name later arrives from another device via sync, the deletion is honored rather
than the paycheck being silently resurrected. This is the mechanism that makes deletes propagate
correctly in a last-write-wins system with no central coordinator.

---

## `SavedPaycheckMerger` — deterministic last-write-wins

The single merge algorithm, used identically **server-side** (merging a client's push against
stored state) and available to clients/tests, so every party resolves conflicts the same way:

```csharp
public static SavedPaycheckSet Merge(SavedPaycheckSet existing, SavedPaycheckSet incoming)
```

Both `existing` and `incoming` contribute both their live entries and their tombstones as
candidates, keyed by name (case-insensitive). For each name, the winner is:

```csharp
public bool Beats(Candidate other)
{
    if (Timestamp != other.Timestamp)
        return Timestamp > other.Timestamp;          // 1. later timestamp always wins
    if (IsTombstone != other.IsTombstone)
        return !IsTombstone;                          // 2. exact tie: a live entry beats a tombstone
    return FromIncoming && !other.FromIncoming;        // 3. same kind, same time: incoming wins
}
```

Rule 2 is a deliberate bias: **deletes never win a timestamp tie.** If a rename and a delete
happen to carry the identical timestamp, the surviving record is the live one — a conservative
choice that avoids accidentally losing data to a coincidental clock collision.

Rule 3 is what makes **re-running the same sync a no-op** — pushing identical data twice does not
flip-flop the winner, because on a genuine tie the incoming (repeated) push simply re-confirms
itself.

Output is sorted by name (case-insensitive) for deterministic, diffable results — useful for
tests and for reasoning about sync behavior.

`BudgetMerger` in `Shared/Budgeting/` is four structurally identical copies of this same
algorithm — one each for budgets (string-keyed), transactions, recurring bills, and savings goals
(all three GUID-keyed). See [08 — Budgeting](08-budgeting.md#sync-model-sharedbudgeting).

---

## JSON configuration (`Shared/Json/`)

### `PaycheckJson`

**The single JSON configuration shared by every part of sync** — the on-device file store, the
HTTP client, and the API server:

```csharp
public static class PaycheckJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);   // camelCase, case-insensitive
        AddConverters(options);
        return options;
    }

    public static void AddConverters(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());       // enums as names, never ordinals
        options.Converters.Add(new StateInputValuesJsonConverter());
        options.Converters.Add(new DateOnlyJsonConverter());          // ISO-8601 for DateOnly
    }
}
```

Using one options object everywhere is what guarantees a snapshot written by one component
deserializes identically in another. `AddConverters` is exposed separately so the API can apply
the exact same converters to ASP.NET's own JSON pipeline:

```csharp
// PaycheckCalculator.API/Program.cs
builder.Services.ConfigureHttpJsonOptions(options => PaycheckJson.AddConverters(options.SerializerOptions));
```

**Enums serialize as names, not ordinals**, specifically so that reordering an enum in a future
change cannot silently corrupt already-stored data — a `PayFrequency` value written as `"Weekly53"`
stays `"Weekly53"` no matter where that member later moves in the declaration.

### `StateInputValuesJsonConverter`

The converter that makes the schema-driven state architecture actually work end-to-end. Recall
from [05](05-state-withholding-engine.md#stateinputvalues) that `StateInputValues.GetValueOrDefault<T>`
starts with `raw is T`. A naive `System.Text.Json` round trip of `Dictionary<string, object?>`
would deserialize every value as `JsonElement` — which matches **no** `T`, ever, so every read
would fall through to the `Convert.*` numeric coercions, which themselves fail for strings and
booleans. State calculator input would quietly stop working after a single JSON round trip.

The converter avoids this entirely by materializing values as real CLR primitives during reads:

```csharp
private static object? ReadValue(ref Utf8JsonReader reader) => reader.TokenType switch
{
    JsonTokenType.String => reader.GetString(),
    JsonTokenType.True   => true,
    JsonTokenType.False  => false,
    JsonTokenType.Null   => null,
    // Integral numbers → int; anything with a fractional part → decimal. A decimal with scale 0
    // (e.g. 2m) serializes as "2" and reads back as int — harmless, since the consuming
    // calculators coerce via Convert.ToDecimal/ToInt32 in GetValueOrDefault<T>.
    JsonTokenType.Number => reader.TryGetInt32(out var i) ? i : reader.GetDecimal(),
    _ => throw new JsonException(...)
};
```

And writes them back out with an explicit `switch` over the concrete CLR type — `null`, `bool`,
`string`, `int`, `long`, `decimal`, `double`, with a `ToString()` fallback for anything else. No
`JsonSerializer.Serialize(raw)` recursion, so there is no risk of round-tripping through
`JsonElement` on the write side either.

### `DateOnlyJsonConverter`

A small, focused converter — `DateOnly` as exact `"yyyy-MM-dd"` strings. Used for
`BudgetTransaction.Date` and `SavingsGoal.TargetDate` in the sync DTOs.

---

## The typed HTTP client (`Shared/Client/`)

### `PaycheckApiClient`

One class, all account and sync calls, shared verbatim by both front-ends:

```csharp
public sealed class PaycheckApiClient
{
    public PaycheckApiClient(HttpClient http, IApiBaseAddressProvider baseAddress, ITokenStore tokens);

    public Task<ApiResult> RegisterAsync(string email, string password, CancellationToken ct = default);
    public Task<ApiResult> LoginAsync(string email, string password, CancellationToken ct = default);
    public Task LogoutAsync(CancellationToken ct = default);
    public Task<ApiResult<SyncResponse>> SyncAsync(SyncRequest request, CancellationToken ct = default);
    public Task<ApiResult<BudgetSyncResponse>> SyncBudgetsAsync(BudgetSyncRequest request, CancellationToken ct = default);
    public Task<ApiResult<string>> ExportAccountDataAsync(CancellationToken ct = default);
    public Task<ApiResult> DeleteAccountAsync(CancellationToken ct = default);
}
```

Every request URI is built **per call** from `IApiBaseAddressProvider.BaseAddress`, never from
`HttpClient.BaseAddress`:

```csharp
private Uri? BuildUri(string relative)
    => _baseAddress.BaseAddress is { } baseAddr ? new Uri(baseAddr, relative) : null;
```

This matters specifically because the MAUI app lets the user edit the server URL at runtime — a
fixed `HttpClient.BaseAddress` set once at construction could never pick up that change; resolving
it fresh on every call can.

**A 401 on a token-bearing call triggers exactly one refresh-and-retry.** The pattern, repeated
for sync, budget sync, export, and delete:

```csharp
var resp = await PostSyncAsync(uri, request, tokens.AccessToken, ct);
if (resp.StatusCode == HttpStatusCode.Unauthorized)
{
    resp.Dispose();
    var refreshed = await TryRefreshAsync(tokens.RefreshToken, ct);
    if (refreshed is null)
        return ApiResult<SyncResponse>.Fail("Your session has expired. Please sign in again.");
    resp = await PostSyncAsync(uri, request, refreshed.AccessToken, ct);
}
```

No retry loop — a second 401 after a successful refresh is treated as a genuine failure, not
retried again. `TryRefreshAsync` itself clears stored tokens on failure, which is what surfaces
as "please sign in again" rather than a silent hang.

**No exception ever escapes to the caller for expected failure modes.** Network failures
(`HttpRequestException`, `TaskCanceledException`) and unsuccessful responses are all converted
into a failed `ApiResult`/`ApiResult<T>`:

```csharp
public sealed record ApiResult(bool Success, string? Error)
{
    public static ApiResult Ok() => new(true, null);
    public static ApiResult Fail(string error) => new(false, error);
}
public sealed record ApiResult<T>(bool Success, T? Value, string? Error);
```

Error messages are extracted from an RFC 7807 `ValidationProblemDetails`-shaped body when present
(joining all field errors), falling back to a generic `"Request failed (400 Bad Request)."`
message otherwise.

### Store and identity abstractions

```csharp
public interface ITokenStore
{
    ValueTask<AuthTokens?> GetTokensAsync(CancellationToken ct = default);
    ValueTask SetTokensAsync(AuthTokens? tokens, CancellationToken ct = default);
}

public interface IApiBaseAddressProvider { Uri? BaseAddress { get; } }

public sealed record AuthTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);
```

| Abstraction | MAUI implementation | Blazor implementation |
|---|---|---|
| `ITokenStore` | `SecureStorageTokenStore` — platform secure storage, survives app restarts | `CircuitAccountSession` — circuit memory only, gone when the tab closes |
| `IApiBaseAddressProvider` | `PreferencesApiBaseAddressProvider` — user-editable server URL in `Preferences` | `ConfigApiBaseAddressProvider` — reads `PaycheckApi:BaseUrl` from configuration, `null` if unset/malformed |

The Blazor choice is the load-bearing privacy decision here: **credentials never touch disk on
the web app.** They live only as long as the SignalR circuit does.

---

## `ISavedPaycheckStore` and `PaycheckSyncService`

```csharp
public interface ISavedPaycheckStore
{
    Task<SavedPaycheckSet> LoadAsync(CancellationToken ct = default);
    Task UpsertAsync(SavedPaycheckDto dto, CancellationToken ct = default);
    Task RemoveAsync(string name, DateTimeOffset deletedAtUtc, CancellationToken ct = default);
    Task ClearAsync(DateTimeOffset deletedAtUtc, CancellationToken ct = default);
    Task ReplaceAllAsync(SavedPaycheckSet set, CancellationToken ct = default);
}
```

Three implementations, identical contract:

| Implementation | Host | Persistence |
|---|---|---|
| `JsonFilePaycheckStore` | MAUI | `saved-paychecks.json` in `FileSystem.AppDataDirectory`, semaphore-guarded read-modify-write, corrupt file moved aside rather than crashing startup |
| `SessionPaycheckStore` | Blazor | In-memory `Dictionary`s scoped to the circuit, mirrored to browser `localStorage` (`paycheckcalc.paychecks.v1`) on every mutation and hydrated on the first interactive render |
| (server rows) | Api | `SavedPaycheckEntity` via EF Core — see [11 — The Sync API](11-sync-api.md) |

`PaycheckSyncService` is the one-shot orchestration used by both clients:

```csharp
public async Task<SyncOutcome> SyncAsync(CancellationToken ct = default)
{
    var local = await _store.LoadAsync(ct).ConfigureAwait(false);
    var result = await _api.SyncAsync(SyncRequest.From(local), ct).ConfigureAwait(false);
    if (!result.Success || result.Value is null)
        return SyncOutcome.Fail(result.Error ?? "Sync failed.");

    var merged = result.Value.ToSet();
    await _store.ReplaceAllAsync(merged, ct).ConfigureAwait(false);
    return SyncOutcome.Ok(merged);
}
```

Load local state → push to the server → **replace local state with whatever the server merged
back**. The merge itself always runs server-side, so **no client implements its own conflict
resolution** — the same `SavedPaycheckMerger.Merge` that runs in `PaycheckSyncEndpoints` is simply
reused (Shared references Core only, and both Api and the clients reference Shared), guaranteeing
identical outcomes regardless of which device initiated the sync.

`SyncRequest`/`SyncResponse` are the wire records:

```csharp
public sealed record SyncRequest(IReadOnlyList<SavedPaycheckDto> Paychecks, IReadOnlyList<SavedPaycheckTombstone> Tombstones)
{
    public static SyncRequest From(SavedPaycheckSet set) => new(set.Paychecks, set.Tombstones);
}
public sealed record SyncResponse(IReadOnlyList<SavedPaycheckDto> Paychecks, IReadOnlyList<SavedPaycheckTombstone> Tombstones, DateTimeOffset ServerTimeUtc)
{
    public SavedPaycheckSet ToSet() => new(Paychecks, Tombstones);
}
```

---

## Entitlements

```csharp
public interface IEntitlementProvider { bool IsPro { get; } }
public sealed class FreeEntitlementProvider : IEntitlementProvider { public bool IsPro => false; }
```

Covered fully in [08 — Budgeting](08-budgeting.md#entitlements-and-pro-gating) — the single gate
behind the budget-report feature, currently hard-wired to the free tier in both front-ends pending
a future billing integration.

---

## MAUI-only orchestration: `SyncCoordinator`

Not in Shared (it depends on MAUI's `Connectivity`/`MainThread`), but the natural companion to
everything above — `App/Services/Sync/SyncCoordinator.cs` implements
`App/Services/Sync/ISyncCoordinator.cs`:

```csharp
public interface ISyncCoordinator
{
    event EventHandler<SyncOutcome>? SyncCompleted;
    Task<bool> IsSignedInAsync();
    void RequestSync();                                              // fire-and-forget, coalesced
    Task<SyncOutcome> SyncNowAsync(CancellationToken ct = default);   // awaitable
}
```

Two behaviors worth knowing:

**Coalescing.** `RequestSync()` is meant to be called opportunistically (after every save, every
edit) without the caller worrying about spamming the network:

```csharp
public void RequestSync()
{
    if (Interlocked.Exchange(ref _autoSyncQueued, 1) == 1) return;   // one already queued/running — drop this one
    _ = Task.Run(async () => { try { await SyncNowAsync(); } finally { Interlocked.Exchange(ref _autoSyncQueued, 0); } });
}
```

**Serialization.** `SyncNowAsync` itself is gated by a `SemaphoreSlim(1, 1)` so overlapping calls
never race against each other, and every exception path converts to a `SyncOutcome.Fail(...)`
rather than throwing — including a connectivity pre-check
(`Connectivity.Current.NetworkAccess != NetworkAccess.Internet`) before even attempting the
network call.

`CalculatorViewModel` subscribes to `SyncCompleted` in its constructor to refresh the saved-
paychecks list whenever a sync lands, from any trigger (explicit "Sync Now" or an opportunistic
`RequestSync()` after a local change).

---

**Next:** [11 — The Sync API](11-sync-api.md)
