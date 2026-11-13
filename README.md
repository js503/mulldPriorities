# mulld Priorities

Colony needs and work priorities in one screen, for **RimWorld 1.7 / Waylith**.

The local repository folder is deliberately named **muldPriorities**. The player-facing name is **mulld Priorities**, with a lowercase brand and a capitalized feature name.

## Play

1. Copy this folder's `About`, `Defs` and `Assemblies` directories into `RimWorld/Mods/muldPriorities`, or run `tools/Install.ps1`.
2. Enable **mulld Priorities** in RimWorld's Mods menu and restart.
3. Open the **mulld Priorities** main tab while viewing a colony map.
4. Select a need to see its evidence and suggested change. Use **Stage** to add the suggestion to a draft, or **Edit** a work row to set individual priorities.
5. **Review changes**, then **Apply reviewed changes**. Applying never claims that the underlying need has already been solved.

If numbered work priorities are off, the review explicitly offers to enable them for the save, across all maps. Nothing changes until you approve the review. **Discard** clears a draft; **Undo apply** reverses the last applied batch if its assignments have not since changed or become protected.

**Automation is off by default.** Enabling the mod alone does not change existing work assignments.

## Main interface

- Food runway, care coverage and harvest pressure above the work overview.
- **All needs** also exposes hauling backlog, uncovered essential roles and competing commitments.
- Selecting an indicator highlights its related work row. Long evidence and assignment lists have tooltips.
- Every visible `WorkTypeDef` becomes a row, including work added by other mods.
- Expand a row to edit each colonist's priority, lock assignments and configure a staffing rule.
- **Colonists** excludes individual workers from management.
- **Settings** controls food targets, main-job limits and opt-in daily rule assignment.

### Staffing rules

Rules select a count of the best workers, a percentage of eligible workers, everyone or nobody. Percentages round up. Ranking uses relevant skills, optional passion weighting, current main-job load and a stable tie-breaker. Rules can specify minimum skill, adults only and priority 1–4.

Rules start disabled. **Colony preset → Balanced colony** configures rules without applying them. **Stage rule assignments** calculates a reviewable draft and preserves separately staged manual edits and need suggestions. Existing manual assignments outside enabled rules are preserved and count toward the main-job cap.

Daily automation applies enabled staffing rules every 60,000 game ticks. It does **not** automatically accept need recommendations. It respects exclusions, locks, active boosts, incapabilities, age restrictions and main-job limits. Standing, undrafted colonists outside mental states are considered available; sleeping colonists can still be selected. Temporarily unavailable workers retain their existing assignments.

Firefighting, patient and bed rest work are exempt from the main-job cap. An underfilled rule is possible when too few eligible workers remain after protections and caps.

### Temporary boosts

Need suggestions adjust one colonist's assignment temporarily. Food, harvest, hauling and essential-coverage boosts last at most one in-game day; care boosts last at most half a day. A boost restores its previous priority when its condition resolves or its timer expires. Checks run once per in-game hour.

If another mod or the player changes the priority, restoration does not overwrite that edit. Newly locked or excluded assignments are also preserved. An off-map colonist is checked when they return. Boost records, locks, exclusions, rules, automation settings and undo history are stored per map in the save.

### Indicator definitions and limits

| Indicator | Measurement | How it can inform priorities |
|---|---|---|
| Food runway | Known, unforbidden prepared-food nutrition on the map divided by fed-state daily food demand of spawned free colonists | Suggests a cook boost below the chosen target when raw food is present |
| Care coverage | Spawned colonists, prisoners and slaves requiring player tending versus assigned, available free-colonist doctors who are not patients themselves | Suggests another doctor when patient count exceeds assigned available doctors |
| Harvest pressure | Harvestable sown plants; flags dying plants and plants currently below freezing | Suggests another grower; no forecast or harvest-duration claim |
| Hauling backlog | Known, unforbidden entries in the game's potential-hauling list, with change since the previous sample | Suggests hauling capacity; the count is not a list of confirmed executable jobs |
| Critical coverage | Firefighting, doctoring or cooking without an assigned available colonist | Suggests a capable backup |
| Competing commitments | Colonists with more priority 1–2 non-emergency jobs than the chosen cap | Identifies assignments to redistribute; does not invent a utilization percentage |

Food is a stock estimate, not an individualized diet or reachability forecast. Bills, fuel, food policies, allowed areas, storage capacity, hazards and pathfinding can block work despite adequate staffing. Raw nutrition is not proof that a particular cooking bill has usable ingredients. No recommendation overrides incapability or automatically creates bills/designations.

## Presets

**Colony preset → Export rules / Import rules** uses `mulldPriorities-preset.xml` in RimWorld's current save-profile `Config` folder. Import validates the complete file before replacing rules, skips absent work definitions and leaves current priorities, exclusions and locks alone. Export stores staffing rules only.

## Build

No third-party libraries, Harmony, HugsLib or network restore are required.

```powershell
pwsh -NoProfile -File ./tools/Build.ps1
pwsh -NoProfile -File ./tools/Test.ps1
```

The offline build uses Roslyn and reference assemblies bundled with **PowerShell 7.6+ / .NET 10**. Set `-GamePath` if RimWorld is installed elsewhere. Output: `Assemblies/Mulld.Priorities.dll`.

Alternatively, with the .NET 10 SDK:

```powershell
dotnet build ./Source/Mulld.Priorities.csproj -c Release -p:GamePath="C:\Path\To\RimWorld"
```

Game assemblies are referenced locally and are never redistributed. The build targets the installed 1.7 assemblies directly, not Unity-era assumptions.

## Validation

`tools/Test.ps1` runs policy checks and parses both XML definition files. The opt-in in-game harness exercises real pawn assignments, stale-plan rejection, protection rules, expiry, preset import/export and save/reload, and captures the overview, editor, colonists, settings and review views.

```powershell
pwsh -NoProfile -File ./tools/Build.ps1 -WithSmokeTests
pwsh -NoProfile -File ./tools/Install.ps1
pwsh -NoProfile -File ./tools/Smoke-Test.ps1
```

The smoke test launches a new `-quicktest` colony using the isolated `artifacts/smoke-profile` save folder. Its requested mod list contains Core and this mod; RimWorld's quicktest also activates the installed official expansions. It does not use your normal saves or active-mod configuration. Results and screenshots are written there. The game exits when the test finishes. The test build executes the harness only when explicitly launched with `-mulldsmoketest`.

After testing, rebuild **without** `-WithSmokeTests` and reinstall for normal use. The release DLL excludes the harness.

## Compatibility and current scope

- Target: RimWorld **1.7.5005**, with baseline priority values Off/1/2/3/4.
- Independent implementation; no AutoPriorities code or assets copied.
- Current-map free colonists are managed. Slaves/prisoners may count as patients but are not automatically assigned work.
- Other work managers can change the same assignments. Conflict checks protect review and undo; avoid enabling competing daily managers for the same colonists.
- Expanded-priority mods, multiplayer, special race work systems and every modded food category have not been validated.
- Interface text is English; a complete translation pack and Steam publishing are not included.

This is the first playable local build, rather than a claim of compatibility with every colony or mod list.
