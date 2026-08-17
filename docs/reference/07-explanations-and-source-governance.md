# 07 — Explanations & Source Governance

Two closely related subsystems: the **"Show Your Work"** object model that lets any result
explain itself line by line, and the **source manifest** that ties every explanation back to a
verified, citable official publication.

---

## The "Show Your Work" model

`Core/Explanation/` — four types, all `sealed`, most `record`.

### `ExplanationStep`

```csharp
public sealed record ExplanationStep(
    string Label,
    string Detail,
    decimal? Value = null,
    string? Formula = null);
```

One line of a worksheet. `Value` is nullable because a purely informational step (like a "No
withholding" message) may carry no computed amount. `Formula` renders the arithmetic verbatim
with real numbers substituted — `"$5,000 × 26 = $130,000"` — so the step is checkable by hand.

### `LineExplanation`

```csharp
public sealed record LineExplanation(
    ExplanationLineKey Key,
    string Title,
    decimal FinalAmount,
    IReadOnlyList<ExplanationStep> Steps,
    string? Reference = null,
    IReadOnlyList<string>? SourceRuleIds = null);
```

Everything needed for one modal: which line it explains (`Key`), a display title, the amount
shown on the paycheck itself (`FinalAmount`, included so the modal heading doesn't need a second
lookup), the ordered steps, a legacy free-text `Reference`, and — the newer, structured path —
`SourceRuleIds` pointing into the manifest.

### `ExplanationLineKey`

```csharp
public enum ExplanationLineKey
{
    GrossPay, FederalTaxableIncome, FicaTaxableWages, StateTaxableWages,
    PreTaxDeductions, FederalWithholding, SocialSecurity, Medicare,
    AdditionalMedicare, StateWithholding, StateDisability, NetPay
}
```

The UI looks explanations up **by key**, never by list position — necessary because the line
count varies (Additional Medicare and State Disability are conditional).

### `PaycheckExplanation`

The aggregate attached to every result (`PaycheckResult`, `BonusResult`, `SelfEmploymentResult`):

```csharp
public sealed class PaycheckExplanation
{
    public PaycheckExplanation(IReadOnlyList<LineExplanation> lines, TaxSourceCatalog? sourceCatalog = null)
    {
        Lines = lines;
        _byKey = lines.ToDictionary(l => l.Key);

        Sources = lines
            .SelectMany(line => ResolveSources(line, sourceCatalog))
            .GroupBy(source => source.RuleId ?? source.Reference, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        AccuracyNotes = Sources
            .SelectMany(source => source.Approximations.Select(n => new AccuracyNote("Approximation", n))
                .Concat(source.Exclusions.Select(n => new AccuracyNote("Exclusion", n))))
            .Distinct()
            .ToList();
    }

    public LineExplanation? Get(ExplanationLineKey key) => _byKey.TryGetValue(key, out var line) ? line : null;
    public static PaycheckExplanation Empty { get; } = new(Array.Empty<LineExplanation>());
}
```

Three things happen at construction, once, so consumers never recompute them:

1. **Lines are indexed by key** for `Get`.
2. **Sources are deduplicated** across all lines — several lines can cite the same publication
   (e.g. the same state's regular-withholding rule backs both the taxable-income line and the
   withholding line), and the result should list it once.
3. **`AccuracyNotes` are derived from the resolved sources**, not hand-maintained. Every rule's
   `Approximations` and `Exclusions` flow automatically into user-facing notes, deduplicated.

`ResolveSources` prefers the structured path (`SourceRuleIds` + the catalog) and falls back to
the legacy free-text `Reference` only when no catalog or no rule IDs are present:

```csharp
if (sourceCatalog is not null && line.SourceRuleIds is { Count: > 0 })
{
    foreach (var id in line.SourceRuleIds)
        yield return sourceCatalog.CreateCitation(line.Title, sourceCatalog.GetById(id));
    yield break;
}
if (!string.IsNullOrWhiteSpace(line.Reference))
    yield return new SourceCitation(line.Title, line.Reference);
```

`PaycheckExplanation.Empty` is the safe default every result type initializes to, so a result
built without an explanation still behaves correctly rather than null-referencing.

### `SourceCitation`

```csharp
public sealed record SourceCitation
{
    public SourceCitation(string label, string reference) { ... }              // legacy, free-text
    internal SourceCitation(string label, TaxSourceRule rule) { ... }           // structured, from the manifest

    public string Label { get; }
    public string Reference { get; }
    public string? RuleId { get; }
    public string PublicationTitle { get; }
    public string? OfficialUrl { get; }
    public int? TaxYear { get; }
    public DateOnly? RevisionDate { get; }
    public DateOnly? EffectiveDate { get; }
    public DateOnly? LastVerificationDate { get; }
    public TaxRuleImplementationType? ImplementationType { get; }
    public IReadOnlyList<string> Approximations { get; } = Array.Empty<string>();
    public IReadOnlyList<string> Exclusions { get; } = Array.Empty<string>();
    public string? ApplicabilityNotes { get; }
}
```

Two constructors, two eras of the codebase, one type. The `internal` rule-based constructor is
only reachable from within Core (via `TaxSourceCatalog.CreateCitation`), so a citation with rich
metadata always traces back to a validated manifest entry — nothing outside Core can fabricate
one.

### `AccuracyNote`

```csharp
public sealed record AccuracyNote(string Title, string Description);
```

Used two ways: derived automatically from source-rule metadata (as above), and attached directly
by a mode-level calculator (`BonusResult.AccuracyNotes`, `SelfEmploymentResult.AccuracyNotes`,
`AnnualProjection.AccuracyNotes`) for assumptions that are not tied to one specific rule — "the
current paycheck is assumed to repeat for every remaining period," for instance.

---

## `TaxSourceCatalog`

`Core/Tax/Sources/TaxSourceCatalog.cs` — the runtime source of truth for provenance. Loaded once
in `AddPaycheckCalculatorCore` from `tax_source_manifest_2026.json`.

### The manifest shape

```csharp
public sealed class TaxSourceManifest
{
    public int TaxYear { get; init; }
    public IReadOnlyList<TaxSourceRule> Rules { get; init; } = Array.Empty<TaxSourceRule>();
}

public sealed class TaxSourceRule
{
    public string Id { get; init; } = "";
    public string Jurisdiction { get; init; } = "";        // "US" or a UsState name
    public int TaxYear { get; init; }
    public TaxRuleScope Scope { get; init; }
    public IReadOnlyList<TaxCalculationMode> CalculationModes { get; init; } = [];
    public string PublicationTitle { get; init; } = "";
    public string OfficialUrl { get; init; } = "";
    public DateOnly? RevisionDate { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public DateOnly LastVerificationDate { get; init; }
    public TaxRuleImplementationType ImplementationType { get; init; }
    public string CalculatorClass { get; init; } = "";
    public IReadOnlyList<string> Approximations { get; init; } = [];
    public IReadOnlyList<string> Exclusions { get; init; } = [];
    public string ApplicabilityNotes { get; init; } = "";

    public bool AppliesTo(UsState state) =>
        string.Equals(Jurisdiction, state.ToString(), StringComparison.OrdinalIgnoreCase);
}
```

The three controlled-vocabulary enums:

```csharp
public enum TaxRuleScope
{
    FederalWithholding, SocialSecurityMedicare, AdditionalMedicare, RegularWithholding,
    PayrollAssessment, SupplementalWithholding, SelfEmploymentTax, EstimatedPayments
}

public enum TaxCalculationMode { Standard, GrossUp, AnnualProjection, Bonus, SelfEmployment }

public enum TaxRuleImplementationType
{
    JsonTable, CodedFormula, FlatRate, PercentageElection,
    NoIncomeTaxAdapter, UnsupportedRegularAggregateSupplementalMethod
}
```

A rule's `AppliesTo(UsState)` compares the jurisdiction string against the enum's own `ToString()`
— which is exactly why `UsState` member names must stay stable (see
[02 — Core Domain Model](02-core-domain-model.md#usstate)).

### Loading and the validation gauntlet

`TaxSourceCatalog.Load(string json)` runs two independent passes before the catalog is usable.

**Pass 1 — raw JSON shape** (`ValidateRawMetadata`), before deserialization touches the typed
model:

```csharp
string[] required =
[
    "id", "jurisdiction", "taxYear", "scope", "calculationModes",
    "publicationTitle", "officialUrl", "lastVerificationDate",
    "implementationType", "calculatorClass", "approximations",
    "exclusions", "applicabilityNotes"
];
```

Every rule object must carry all of these, plus at least one of `revisionDate` /
`effectiveDate`. Every date field present must parse as **exact `yyyy-MM-dd`** — not merely
`DateOnly`-parseable, but that exact ISO format, string length 10.

**Pass 2 — typed validation** (`Validate(TaxSourceManifest)`), after deserialization:

- Manifest tax year must be `TaxYearSupport.IsSupported`.
- The manifest must contain at least one rule.
- No blank `Id`, `Jurisdiction`, `PublicationTitle`, `OfficialUrl`, `CalculatorClass`, or
  `ApplicabilityNotes`.
- **Rule IDs are globally unique**, case-insensitively.
- Every rule's `TaxYear` matches the manifest's year and is itself supported.
- `CalculationModes` is non-empty.
- `Scope`, `ImplementationType`, and every mode in `CalculationModes` are defined enum values —
  guards against a typo'd string surviving deserialization as an unrecognized numeric value.
- `Approximations` and `Exclusions` are non-null (an empty list is fine; null is not — it forces
  every rule to make an explicit statement, even if that statement is "none").
- A revision date or effective date is present.
- `LastVerificationDate` is not the default (unset) value.
- `OfficialUrl` parses as an **absolute HTTPS URL** — `http://` is rejected.
- `Jurisdiction` is either the literal `"US"` or a name that parses as a `UsState`.

Finally, coverage completeness:

```csharp
foreach (var state in Enum.GetValues<UsState>())
{
    if (!manifest.Rules.Any(rule =>
            rule.AppliesTo(state)
            && rule.Scope == TaxRuleScope.RegularWithholding
            && rule.CalculationModes.Contains(TaxCalculationMode.Standard)))
    {
        throw new InvalidOperationException($"Tax source manifest is missing regular-withholding coverage for {state}.");
    }
}
```

A manifest missing even one jurisdiction's baseline withholding citation fails to load —
**before any calculation runs.** This is a hard startup gate, not a warning.

### Calculator-registration cross-check

Separately, `ValidateCalculatorRegistrations(StateCalculatorRegistry)` (called from
`AddPaycheckCalculatorCore` right after both the catalog and the registry are built) asserts that
the manifest's `calculatorClass` for each state's `RegularWithholding` rule matches the runtime
type name of whatever is actually registered. This is what turns "someone renamed
`OldStateCalculator` to `NewStateCalculator` but forgot the manifest" into a startup crash
instead of a silently mislabeled citation. Detailed in
[05 — The State Withholding Engine](05-state-withholding-engine.md#coverage-reconciliation).

### Lookup surface

```csharp
public TaxSourceRule GetById(string id);
public IReadOnlyList<TaxSourceRule> Find(string jurisdiction, int taxYear, TaxRuleScope scope, TaxCalculationMode mode);
public IReadOnlyList<TaxSourceRule> Find(UsState state, int taxYear, TaxRuleScope scope, TaxCalculationMode mode);
public IReadOnlyList<string> RuleIds(string jurisdiction, int taxYear, TaxRuleScope scope, TaxCalculationMode mode);
public IReadOnlyList<string> RuleIds(UsState state, int taxYear, TaxRuleScope scope, TaxCalculationMode mode);
internal SourceCitation CreateCitation(string label, TaxSourceRule rule);
```

Every calculator (`PayCalculator`, `BonusCalculator`, `SelfEmploymentCalculator`) calls
`RuleIds(...)` with its own `TaxCalculationMode` when attaching sources to a `LineExplanation` —
which is how the same California SDI rule can be tagged `Standard` for a regular paycheck and
simply not apply in `Bonus` mode (since disability lines are excluded there).

### Manifest scale

112 rules across 8 scopes at time of writing:

| Scope | Count |
|---|---|
| `SupplementalWithholding` | 52 |
| `RegularWithholding` | 51 |
| `PayrollAssessment` | 4 |
| `EstimatedPayments` | 1 |
| `SelfEmploymentTax` | 1 |
| `AdditionalMedicare` | 1 |
| `SocialSecurityMedicare` | 1 |
| `FederalWithholding` | 1 |

And by implementation type:

| Implementation | Count |
|---|---|
| `CodedFormula` | 44 |
| `FlatRate` | 21 |
| `UnsupportedRegularAggregateSupplementalMethod` | 21 |
| `NoIncomeTaxAdapter` | 18 |
| `JsonTable` | 6 |
| `PercentageElection` | 2 |

A sample rule (federal self-employment estimated payments):

```json
{
  "id": "federal-form-1040es-2026",
  "jurisdiction": "US",
  "taxYear": 2026,
  "scope": "EstimatedPayments",
  "calculationModes": ["SelfEmployment"],
  "publicationTitle": "2026 Form 1040-ES, Estimated Tax for Individuals",
  "officialUrl": "https://www.irs.gov/pub/irs-pdf/f1040es.pdf",
  "effectiveDate": "2026-01-01",
  "lastVerificationDate": "2026-08-08",
  "implementationType": "CodedFormula",
  "calculatorClass": "SelfEmploymentCalculator",
  "approximations": ["The calculator splits annual estimates into four equal installments."],
  "exclusions": ["State installment schedules and filing-time adjustments can differ from the federal schedule."],
  "applicabilityNotes": "Standard federal estimated-payment installment dates."
}
```

---

## The spreadsheet vs. the manifest

`docs/US_State_Withholding_Tax_Sources_2026.xlsx` is the **provenance and working inventory** —
a research artifact, not something the app reads. `tax_source_manifest_2026.json` is the
**runtime source of truth**. When updating a source, the JSON is what matters for the app; the
spreadsheet is where the research trail lives for humans.

---

## User-facing disclosure surfaces

Both front-ends expose the resolved `Sources` and `AccuracyNotes` on an "Accuracy & Sources"
screen/modal, scoped to **only the sources actually used by the active calculation mode and
selected state** — a California standard-paycheck run does not show Vermont's supplemental rule.

Exports carry the same discipline: the per-period and annual PDF/CSV exports include citations
for the calculation they represent, but the **standalone A/B comparison export intentionally
carries no citations**, because a side-by-side comparison of two saved paychecks is not itself
one calculation context with one coherent source list.

---

## Reporting and correcting an accuracy issue

### Filing a report

[`.github/ISSUE_TEMPLATE/accuracy-incident.yml`](../../.github/ISSUE_TEMPLATE/accuracy-incident.yml)
structures every report with: jurisdiction, tax year, calculation mode, app surface/version,
reproduction inputs (fictional/redacted only — the form explicitly warns against real personal or
payroll data), actual vs. expected result, the official publication URL, its revision, the exact
page/table/worksheet, its effective date, a severity rating, and suspected scope.

### The eight-step correction process

From [`docs/wiki/Accuracy-and-Source-Governance.md`](../wiki/Accuracy-and-Source-Governance.md):

1. **Triage and reproduce** with non-sensitive inputs on the reported surface and release.
2. **Confirm authority and period** — verify the controlling publication, revision, exact
   location, and effective dates.
3. **Determine scope** — which rules, jurisdictions, modes, result lines, date ranges, releases.
4. **Add a failing regression test** with explicit expected values taken from the publication —
   never derived by running production code.
5. **Correct implementation and provenance together** — code/data changes ship with the matching
   manifest metadata and verification-date update.
6. **Peer-review the legal interpretation**, not just the diff, against the official source.
7. **Validate and communicate** — run the full suite; publish release notes describing impact,
   affected periods, and whether recalculation is advised.
8. **Close with traceability** — record the fix version and an affected-calculation summary on
   the incident.

**When the publication is ambiguous, preserve existing behavior** until the interpretation can be
verified — and never silently broaden a fix beyond its confirmed effective period. This is the
same spirit as the "if a tax rule looks odd, check the matching test before changing it" rule in
`CLAUDE.md`.

---

**Next:** [08 — Budgeting](08-budgeting.md)
