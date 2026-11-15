# mulld Priorities

Colony needs and work priorities together inside RimWorld 1.7's **Work** tab.

## Use

1. Enable Harmony and **mulld Priorities**, then restart RimWorld.
2. Open **Work**. The segmented **mulld Priorities / chevron** control expands or collapses the extension above the native grid.
3. Use the unified preset dropdown to choose a preset. **Rules**, **Colonists** and **Settings** stay below the always-visible colony KPIs.
4. Click **Preview Changes**. Gold-outlined grid cells show the proposed number at full size; click one to see its current → proposed value in the review row. Other assignments remain current.
5. **Apply N Changes** approves the draft. Preview, preset selection and preset saving never apply work assignments themselves.

The compact rule table uses Previous/Next pages. **Edit…** opens minimum skill, backup priority, age, passion and focus options. Page size adapts to screen height.

**Save Preset…** opens **Save Rules As**. Enter a custom name; existing names require an explicit, compact overwrite confirmation. Presets include rules, the primary-job limit and policy. They do not contain colonist exclusions, locks, temporary boosts, food targets or the daily-automation switch. A changed rule invalidates the draft and marks the current preset with `*`.

When applying from checkbox mode, undo preserves the stored numbered assignments revealed by the switch. Discard clears the draft. Undo reverses the last applied batch when its assignments have not since changed or become protected. If numbered priorities are disabled, review explicitly offers to enable them for the save across all maps.

Automation starts off. Existing saved custom rules keep their previous behavior until you load a preset or explicitly enable the balanced policy.

## Balanced assignments

Balanced plans allocate skilled primary workers while distributing their main-job load. Doctoring, cooking, growing and construction get coverage first. Firefighting, patient care and bed rest remain priority 1 for capable managed colonists. Basic tasks and urgent hauling do not consume a specialist's main-role allowance.

Most productive roles receive a separate backup at priority 3 and capable fallback workers at 4. When the main-role cap is full, essential coverage remains enabled as support work. Available managed workers without a primary receive useful hauling or cleaning work where permitted. Small colonies may need essential support jobs beyond their primary-role limit.

Hunting, dark study and unfamiliar mod work retain manual assignments. Locks, exclusions, incapabilities, age restrictions and active boosts remain protected. Temporarily unavailable pawns retain their assignments. Coverage means enabled workers, not guaranteed execution: bills, fuel, allowed areas, access, designations and hazards still matter.

**Rules** controls primary counts, priorities, backups, fallback work, minimum skill, age and passion weighting. **Colonists** controls exclusions and assignment locks. **Settings** controls targets, primary-job limits, the staffing policy, import/export and optional daily assignment.

Daily rules run every 60,000 game ticks and do not automatically accept need suggestions. If AutoPriorities' per-map Run on timer setting is enabled, mulld pauses daily assignments. Manual review/apply remains available. mulld never changes that mod's settings.

## Needs

- **Food runway:** known, unforbidden prepared nutrition divided by daily colonist demand; raw ingredients are reported separately.
- **Care coverage:** player-tended patients versus available assigned doctors who are not patients themselves.
- **Harvest pressure:** harvestable sown plants, including dying or currently freezing plants.
- **Hauling backlog:** potential-hauling entries and change since the last sample; these are not guaranteed executable jobs.
- **Critical coverage:** firefighting, doctoring or cooking without an available assigned worker.
- **Competing commitments:** workers exceeding the configured primary-role limit; this is assignment count, not measured utilization.

Need boosts last at most one day, or half a day for care, and expire sooner if their condition resolves. Restoration preserves subsequent player/mod edits, locks and exclusions. Settings, rules, boosts, exclusions, locks and undo history persist per map.

Named presets live in `mulldPriorities-presets.xml` in the current save profile's Config folder and can be loaded across colonies in that profile. Names compare without case and are limited to 40 characters. The library loads on demand and writes only on Save/Overwrite, using an atomic file replacement. A malformed library is not silently overwritten; absent mod jobs remain stored and are skipped when loading rules.

Built-ins: **Balanced**, **Clean**, **Harvest** and **Haul**. Focus presets assign their named job at priority 1 to every available eligible managed colonist, bypassing the primary-job limit for that job. Incapabilities, age/skill gates, locks, exclusions and active boosts still apply. Focus stays in the rules until changed; other work may wait. **Factory defaults…** loads original built-in rules without deleting a saved override. Preview and apply afterward, or save again to replace the override.

Single-preset export/import still uses `mulldPriorities-preset.xml`. Format 3 adds the primary-job limit and focus flag; formats 1 and 2 remain readable. Import validates before replacing rules and never applies priorities.

## Build and install

Requires installed RimWorld 1.7 / Waylith, Harmony, and PowerShell 7.6+ / .NET 10. No network restore is needed; Roslyn ships with PowerShell. Run the following commands from the repository root.

```powershell
./tools/Build.ps1
./tools/Test.ps1
./tools/Install.ps1
./tools/Package.ps1
```

Set Build's `-GamePath` and `-HarmonyPath` if installations differ. Alternatively use the .NET 10 SDK and Source/Mulld.Priorities.csproj with those properties. Referenced game/Harmony assemblies are not redistributed.

Install copies About, Defs, Patches, Textures and Assemblies to RimWorld/Mods/mulldPriorities. The repository and installed folder are named **mulldPriorities**; player-facing branding is **mulld Priorities**.

## Validation

Policy tests cover distribution, primary limits, essential support, backups, fallbacks and protection rules. The diagnostic build tests actual game assignments, save/reload, storage food counting, other-manager overlap and the integrated interface.

```powershell
./tools/Build.ps1 -WithSmokeTests
./tools/Install.ps1
./tools/Smoke-Test.ps1 -CurrentModList -ProfileName integration-check -SavePath 'C:\Path\To\Colony.rws'
```

Tests use a disposable profile and copied save, then exit. Rebuild without `-WithSmokeTests` and reinstall afterward. See ../Tests/Audit.md, Tests/Integration.md and Tests/Compatibility.md for results and limits.

Targets standard Off/1/2/3/4 priorities. Native work columns are retained, including mod columns. Current-map free colonists are managed; slaves and prisoners may count as patients. Multiplayer, expanded priority scales and every race/work-manager combination have not been validated. English interface; independent implementation, with no AutoPriorities code or assets copied.

## GitHub publication

The configured destination is js503/mulldPriorities. Once the official GitHub CLI is installed and authenticated with `gh auth login`, run `./tools/Publish-GitHub.ps1`. It creates a private repository if absent, pushes committed history to main without force, and configures upstream tracking. It refuses a different owner, origin or public destination. Credentials are never stored in the repository.

## Performance diagnostics

Open Options → Mod Settings → mulld Priorities and enable **Performance diagnostics**. It is off by default and persists globally. The panel shows calls, average/worst milliseconds and average/worst managed KiB allocated for needs scans, planning, validation, apply, automation and the mulld panel's Repaint. Cached needs reads are counted separately. Reset clears all counters; disabling stops recording immediately.

Counters use fixed storage and stack/value scopes. Disabled instrumentation does not query timestamps or allocation counters and allocates zero bytes after initialization. There is no sampling thread, growing history or continuous logging. The display formats values at most once per second while open. Nested operation times overlap; allocations are bytes created on the calling thread, not retained RAM or a per-mod CPU percentage.

See ../Tests/Performance.md for the reproducible 100-colonist benchmark, limitations and before/after measurements. Benchmark generation lives only in diagnostic builds. Use an isolated runtime when your normal game is running:

```powershell
./tools/Build.ps1 -WithSmokeTests
./tools/New-TestRuntime.ps1
./tools/Smoke-Test.ps1 -GamePath ./artifacts/test-runtime -CurrentModList -Benchmark -ProfileName benchmark -SavePath 'C:\Path\To\Colony.rws'
```

The benchmark creates a 100-colonist fixture under its disposable profile, warms the operations, measures 64 samples with diagnostics off/on and captures 120 panel repaints per view. It disables tiered JIT only for that benchmark child process. It never changes your normal game settings. Rebuild the release without diagnostics harnesses before installing for play.

