# Compact Ledger — 0.4.2 validation

The approved Compact Ledger combines three neutral KPI rows, aligned tabs, a unified preset dropdown, labeled Save Preset, and a four-button review bar. Preview Changes is immediately left of Discard Changes. Advanced rule options live in Edit; native work columns and proposed-number review remain intact.

## Results

- 67 standalone checks passed: 44 policy, 7 diagnostics, 13 named presets and 3 settings migration checks.
- 330 in-game checks passed at each of 1280×900 and 1280×720 using the current 99-entry mod configuration and a copied five-colonist save.
- All 26 visible work types were present, including Repair Gear. Existing assignment audits exercise eligible work types in both staffing policies, along with locks, exclusions, preview isolation, apply/undo, save/reload and preset overwrite.
- No managed game errors during the final load/reload and interface checks. External engine/ReShade startup warnings are outside this assertion.
- Inspected screenshots of the rules, last page, advanced options, colonists, settings, preview, applied assignments, collapsed table and preset dialogs.
- At 1280×900, eight rules and all five colonists fit. Shorter displays reduce the rule page size and retain native pawn-table scrolling.

The new rule editor exposes minimum skill, backup priority, adult-only eligibility, passion preference and focus. Rule changes continue to invalidate pending drafts; editing or saving rules does not apply pawn priorities.

## Reproduction

Build with `tools/Build.ps1 -WithSmokeTests`, prepare `tools/New-TestRuntime.ps1`, then run `tools/Smoke-Test.ps1 -GamePath ./artifacts/test-runtime -CurrentModList -ProfileName ledger-check -SavePath <copy-source.rws>`. Add `-ScreenHeight 720` for the shorter layout.

Raw logs and screenshots remain in ignored `artifacts/ledger-release-900` and `artifacts/ledger-release-720`. The normal configuration and source save hashes were verified unchanged. Release assemblies are rebuilt without the smoke harness before installation and packaging.

This verifies assignment and UI compatibility with the tested list, not every behavior of every third-party work giver.
