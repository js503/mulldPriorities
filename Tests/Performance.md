# Performance diagnostics and large-colony benchmark — 0.3.0

Fixture: 100 actual spawned free colonists, 39,747 map things, 4,001 potential-hauling stacks, 2,000 ripe sown crops, 25 visible work types, all 97 requested mod entries loaded. Generated from a disposable copy of Test.rws, then both measured builds load exactly the same saved fixture. Every proposed plan contains 677 changes. The original save and normal running game are not benchmark outputs.

.NET 10.0.15, 16 logical processors. Both measured processes disable tiered compilation locally, warm each operation 8 times, then measure 64 samples with diagnostics off and again with it on. Simulation is paused; these are warmed elapsed-time/managed-allocation operation benchmarks, not an end-to-end TPS/FPS or CPU-share claim. The user's normal game was running concurrently, so scheduling noise affects elapsed-time tails. No forced GC is used. Full median/p95/worst times, allocated bytes and Gen0 collections are in Performance/before.json and after.json.

## Operations, diagnostics off

| Operation | Before median ms | After median ms | Allocated KiB before → after |
|---|---|---|---|
| NeedsScan | 2.586 | 1.783 | 299 → 35 |
| Plan | 5.465 | 1.515 | 2146 → 628 |
| Validate | 0.170 | 0.108 | 287 → 52 |
| ApplyUndo | 0.336 | 0.179 | 793 → 100 |
| DailyApplyUndo | 5.774 | 1.799 | 2939 → 729 |

## Open-panel Repaint, diagnostics on

120 samples per view after 30 warmup frames. Measures only mulld's extension; the native work table and other mods' drawing are excluded.

| Panel | Before average ms | After average ms | Allocated KiB before → after |
|---|---|---|---|
| panelNeeds | 0.197 | 0.065 | 7.7 → 1.2 |
| panelColonists | 1.190 | 0.159 | 95.6 → 11.7 |
| panelRules | 0.491 | 0.117 | 18.1 → 3.4 |
| panelReview | 0.332 | 0.065 | 21.6 → 1 |

Waiting daily automation and cached needs reads allocate zero bytes in both builds, measured in batches of 10,000. Very short operation timing is dominated by measurement/JIT/scheduling noise; the zero-allocation result is the useful finding. Enabled diagnostics use fixed counters and no growing buffers. Timestamps and allocation calls run only when enabled.

## Bottlenecks fixed and correctness

- Repeated per-slot candidate ranking: now rank once per work type. The pure planner exactly matches the previous implementation across 500 seeded mixed-rule scenarios, in addition to 200 policy scenarios.
- Rebuilding workload counts per need candidate: compute once per scan and reuse for indicators and suggestions.
- The game's ThingID getter creates strings: cache IDs for each planning call rather than reconstructing them for each role/assignment.
- Per-frame sorting/allocation and drawing off-screen colonists/rules: count directly, draw visible rows, reuse the three need-card slots and use tuple preview keys.
- Normal apply/undo created redundant copies and performed empty boost bookkeeping: retain the owned cloned batch and bypass empty boost paths while preserving rollback/protection behavior.

Validation: 44 policy checks, 7 diagnostics checks (including zero allocations with profiling disabled), and 283 in-game checks passed. The in-game suite covers the settings default, persisted toggle, no collection while disabled, real timing/cache counters, every work type in both policies, stale drafts, boosts, undo, locks/exclusions, preset import and save/reload. Both benchmark profiles and the regression finish without managed game errors. Existing third-party/native startup or shutdown warnings remain.

The diagnostics settings panel was visually inspected in-game. Benchmark harnesses, large fixture saves, local runtime copies and screenshots stay under ignored artifacts and are excluded from the release DLL/package. Data and timing are local; nothing is sent anywhere.

Final integrity: all requested package IDs matched loaded IDs in both benchmark profiles and the regression; source save/configuration SHA256 hashes matched after testing. Installed release DLL matches the local release hash. No smoke/benchmark classes are present in the release assembly.
