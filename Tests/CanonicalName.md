# Canonical-name release validation

Validated 2026-11-14 for 0.4.1.

- Renamed repository, installer destination, remote URL and package prefix to `mulldPriorities`.
- Isolated current-mod-list game run: **328 checks passed**, including load/reload and integrated UI views.
- Pure checks: **44 policy, 7 diagnostics, 13 named-preset, 3 settings-migration checks passed**; metadata XML parsed.
- Release assembly rebuilt without smoke-test code; installed to `RimWorld/Mods/mulldPriorities` through the legacy-folder migration.
- Package includes the README, development guide and three actual in-game screenshots.
- Package ID remains `mulld.priorities`. Legacy settings are copied only when no canonical settings file exists.

The existing 0.4.0 performance measurements are retained with their original version and methodology; this naming/documentation release does not claim a new benchmark.
