# Local validation — 2026-11-13

Target installation: RimWorld **1.7.5005 rev1082**, Waylith, Windows.

- **23 pure policy checks passed**, including staffing counts/percentages, stable skill ranking, workload caps, emergency exemptions, locked-on/locked-off assignments, age/skill eligibility and conditional restoration.
- **41 in-game checks passed**, exercising live pawn priorities, stale/invalid plan rejection, locks/exclusions, temporary expiry, external edits, replacement boosts, undo of boosts, daily reassignment, real food/crop/treatment indicators, preset import/export and save/reload.
- Opened and captured the overview, expanded work editor, colonist exclusions, settings and review screens. Inspected the captures for layout and readable labels.
- No game errors recorded during the final load, save/reload and five interface views. A base-game hidden-ritual precept warning appeared in the quicktest profile.
- Verified release assembly identity `Mulld.Priorities, Version=0.1.0.0` and absence of the test harness from the release DLL.
- Rebuilt the release twice and verified identical SHA-256 hashes.
- Parsed the metadata and main-button XML.

The isolated quicktest profile used Core, the five installed official expansions and mulld Priorities. Normal save files and the normal active-mod configuration were not used. Full mod-list compatibility, multiplayer and extended priority ranges remain unvalidated.

Reproduce with `tools/Test.ps1` and the explicit smoke-test workflow in the repository README. The complete in-game result list is in `Tests/in-game-results.txt`.
