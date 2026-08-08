# Accuracy and Source Governance

PaycheckCalculator treats tax rules as versioned legal data. The runtime source of truth for 2026 provenance is [`PaycheckCalculator.Core/Data/tax_source_manifest_2026.json`](../../PaycheckCalculator.Core/Data/tax_source_manifest_2026.json). [`docs/US_State_Withholding_Tax_Sources_2026.xlsx`](../US_State_Withholding_Tax_Sources_2026.xlsx) remains the provenance and working inventory, not a runtime asset.

## Source Manifest Requirements

Every rule records a stable ID, jurisdiction, tax year, scope, calculation modes, official publication and URL, revision or effective date, verification date, implementation type and calculator, applicability notes, approximations, and exclusions.

`TaxSourceCatalog` loads the manifest once through `ITaxDataReader` in `AddPaycheckCalculatorCore`. Startup rejects malformed metadata, unsupported values or tax years, duplicate IDs, missing state/DC regular-withholding coverage, and calculator-class mismatches. The manifest must be packaged with Core, MAUI, Blazor, and Tests whenever its name or location changes.

Only official government sources should be entered. Before changing `lastVerificationDate`, open the official URL, confirm the publication and effective period, and reconcile the manifest record with the implemented rule and tests. Keep historical verification facts traceable in version control.

## Accuracy Disclosures

Explanations resolve stable rule IDs into structured citations. Both applications and their contextual exports show only the sources used by the active calculation mode and selected state. Approximations and exclusions are user-visible accuracy notes, not substitutes for implementing a known rule.

Annual projections are withholding-based estimates rather than tax-return calculations. Bonus and self-employment modes likewise disclose unsupported methods, omitted assessments, liability proxies, and filing-time exclusions. Standalone A/B comparison exports intentionally contain no citations because they do not represent one calculation context.

## Reporting an Incident

Open an [accuracy incident](../../.github/ISSUE_TEMPLATE/accuracy-incident.yml) with fictional or redacted reproduction inputs and the authoritative official publication location. Never include personal, payroll, account, credential, or other sensitive information.

## Correction Process

1. **Triage and reproduce.** Confirm the issue with non-sensitive inputs on the reported surface and release.
2. **Confirm authority and period.** Verify the controlling official publication, revision, page/table/worksheet, and effective dates.
3. **Determine scope.** Identify affected rules, jurisdictions, calculation modes, result lines, date ranges, and releases.
4. **Add a failing regression test.** Use explicit expected values from the publication; do not derive expectations with production helpers.
5. **Correct implementation and provenance.** Update code/data and the corresponding manifest metadata and verification record.
6. **Peer-review the interpretation.** A reviewer must check the legal-rule interpretation against the official publication, not only the code.
7. **Validate and communicate.** Run the complete suite and publish release or advisory notes describing impact, affected periods, and whether recalculation is recommended.
8. **Close with traceability.** Record the fix version and a concise affected-calculation summary on the incident.

If the publication is ambiguous, preserve existing behavior until the interpretation can be verified. Do not silently broaden a correction beyond its confirmed effective period.
