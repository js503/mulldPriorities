# mulld Priorities

Colony needs and work priorities together inside RimWorld 1.7's **Work** tab.

## Use

1. Enable Harmony and **mulld Priorities**, then restart RimWorld.
2. Open **Work** and use the **mulld Priorities** toggle above the grid.
3. Select a need to see its evidence and preview a temporary boost, or choose **Balanced plan** to propose colony-wide assignments.
4. Review gold **current → proposed** cells in the real work grid, then apply. A dash means Off. Priorities stay unchanged until approval.

Discard clears the draft. Undo reverses the last applied batch when its assignments have not since changed or become protected. If numbered priorities are disabled, review explicitly offers to enable them for the save across all maps.

Automation starts off. Existing saved custom rules keep their previous behavior until you select Balanced plan or explicitly enable the balanced policy.

## Balanced assignments

Balanced plans allocate skilled primary workers while distributing their main-job load. Doctoring, cooking, growing and construction get coverage first. Firefighting, patient care and bed rest remain priority 1 for capable managed colonists. Basic tasks and urgent hauling do not consume a specialist's main-role allowance.

Most productive roles receive a separate backup at priority 3 and capable fallback workers at 4. When the main-role cap is full, essential coverage remains enabled as support work. Available managed workers without a primary receive useful hauling or cleaning work where permitted. Small colonies may need essential support jobs beyond their primary-role limit.

Hunting, dark study and unfamiliar mod work retain manual assignments. Locks, exclusions, incapabilities, age restrictions and active boosts remain protected. Temporarily unavailable pawns retain their assignments. Coverage means enabled workers, not guaranteed execution: bills, fuel, allowed areas, access, designations and hazards still matter.

**Rules** controls primary counts, priorities, backups, fallback work, minimum skill, age and passion weighting. **Colonists** controls exclusions and assignment locks. **Settings** controls targets, main-role limits, the balanced policy, import/export and optional daily assignment.

Daily rules run every 60,000 game ticks and do not automatically accept need suggestions. If AutoPriorities' per-map Run on timer setting is enabled, mulld pauses daily assignments. Manual review/apply remains available. mulld never changes that mod's settings.

## Needs

- **Food runway:** known, unforbidden prepared nutrition divided by daily colonist demand; raw ingredients are reported separately.
- **Care coverage:** player-tended patients versus available assigned doctors who are not patients themselves.
- **Harvest pressure:** harvestable sown plants, including dying or currently freezing plants.
- **Hauling backlog:** potential-hauling entries and change since the last sample; these are not guaranteed executable jobs.
- **Critical coverage:** firefighting, doctoring or cooking without an available assigned worker.
- **Competing commitments:** workers exceeding the configured primary-role limit; this is assignment count, not measured utilization.

Need boosts last at most one day, or half a day for care, and expire sooner if their condition resolves. Restoration preserves subsequent player/mod edits, locks and exclusions. Settings, rules, boosts, exclusions, locks and undo history persist per map.

Preset export/import uses `mulldPriorities-preset.xml` in the current profile's Config folder. Format 2 includes balanced and backup settings; format 1 remains readable. Import validates before replacing rules and leaves current priorities unchanged.

## Build and install

Requires installed RimWorld 1.7 / Waylith, Harmony, and PowerShell 7.6+ / .NET 10. No network restore is needed; Roslyn ships with PowerShell.

```powershell
./tools/Build.ps1
./tools/Test.ps1
./tools/Install.ps1
./tools/Package.ps1
```

Set Build's `-GamePath` and `-HarmonyPath` if installations differ. Alternatively use the .NET 10 SDK and Source/Mulld.Priorities.csproj with those properties. Referenced game/Harmony assemblies are not redistributed.

Install copies About, Defs, Patches and Assemblies to RimWorld/Mods/muldPriorities. The local folder is deliberately named **muldPriorities**; player-facing branding is **mulld Priorities**.

## Validation

Policy tests cover distribution, primary limits, essential support, backups, fallbacks and protection rules. The diagnostic build tests actual game assignments, save/reload, storage food counting, other-manager overlap and the integrated interface.

```powershell
./tools/Build.ps1 -WithSmokeTests
./tools/Install.ps1
./tools/Smoke-Test.ps1 -CurrentModList -ProfileName integration-check -SavePath 'C:\Path\To\Colony.rws'
```

Tests use a disposable profile and copied save, then exit. Rebuild without `-WithSmokeTests` and reinstall afterward. See Tests/Integration.md and Tests/Compatibility.md for results and limits.

Targets standard Off/1/2/3/4 priorities. Native work columns are retained, including mod columns. Current-map free colonists are managed; slaves and prisoners may count as patients. Multiplayer, expanded priority scales and every race/work-manager combination have not been validated. English interface; independent implementation, with no AutoPriorities code or assets copied.
