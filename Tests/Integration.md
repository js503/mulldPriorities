# Integrated Work tab validation — 0.2.0

Validated on RimWorld 1.7.5005 / Waylith with the current active mod configuration (97 requested and 97 loaded entries) and a disposable copy of Test.rws (five colonists).

- 35 standalone planner checks passed.
- 64 in-game checks passed: actual assignments, stale-plan protection, locks/exclusions, boosts, preset import/export, save/reload, daily automation overlap, essential coverage and native-grid review/apply.
- Review leaves real priorities unchanged until apply. Applied plans give each available managed pawn primary work and keep qualified essential roles enabled.
- The persistence test deliberately excludes one pawn; review captures therefore show four managed pawns changing among the five visible rows.
- Visually inspected needs panel, multi-pawn gold-cell review, applied grid and collapsed vanilla view. No overlapping review text; native mod columns retained.
- No managed game errors during the tested load/reload/interface sequence. Existing native ReShade shutdown/startup messages and third-party warnings remain outside this mod.

Full assertions: integration-results.txt. Loaded configuration: integration-mods.txt. These checks establish behavior for this configuration, not universal compatibility.

Normal source save and ModsConfig.xml SHA256 hashes matched their pre-test values after validation. Release is rebuilt without diagnostic bootstrap/harness before installation.

