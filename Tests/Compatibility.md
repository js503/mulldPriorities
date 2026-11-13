# Current mod-list compatibility — 2026-11-13

Tested on RimWorld **1.7.5005 rev1082** with the user's **96 active entries**, including Core and five official expansions, plus mulld Priorities. All **97 requested entries** were confirmed in the running-mod manifest. Exact package IDs are recorded in `compatibility-mods.txt`.

## Results

- A fresh colony with the full list passed the original **41 in-game checks**.
- An independent baseline probe with the original list and without mulld Priorities completed with **zero recorded game errors**.
- A copied `Autosave-1.rws` passed **47 in-game checks** with the compatibility fix, including live assignments, undo, temporary boosts, real food/crop/treatment indicators, presets, daily rules, save/reload and five interface views.
- **23 pure policy checks** also passed.
- The final copied-colony run recorded **zero game errors** during load, save/reload and interface testing.
- SHA-256 checks confirmed that the normal `ModsConfig.xml` and original source save were unchanged.

No claims are made about every future scenario, mod update or gameplay combination. These tests cover the currently installed versions and the copied colony; they are not a full simulation of every mod feature.

## Fixed: two timed work managers

The copied colony has AutoPriorities' per-map `RunOnTimer` setting enabled. Running two automatic managers could cause them to overwrite each other's work plans.

mulld now reads that timer through an optional adapter, with no direct DLL dependency. While it is active, mulld **pauses daily rule assignment**, shows the active manager in the header and explains the conflict in Settings. Manual editing and review/apply remain available. mulld never changes AutoPriorities' settings. If the optional timer API cannot be read, daily assignment pauses conservatively.

The in-game tests explicitly enabled the other timer, confirmed that mulld left every assignment and its next-run schedule unchanged, confirmed that the other timer stayed enabled, then disabled it only in the disposable test profile and verified that mulld daily rules resumed.

To use mulld for daily automation in the real colony, turn off **Run on timer / once per day** in AutoPriorities for that map. Your current configuration was left unchanged.

## Verified integrations

- **Adaptive Storage Framework / sbz Fridge:** created a real sbz fridge in the disposable colony, registered meal stacks inside its Adaptive Storage collection, and verified that food runway counted the stored nutrition exactly once. Existing Adaptive Storage items remained visible to the scan; no storage-framework patch was needed.
- **Allow Tool:** additional work types, including `HaulingUrgent`, appeared in the work overview.
- **Combat Training (Continued):** the `TrainingCombat` work type appeared in the overview.
- **Combat Extended and the installed medical/hauling/storage mods:** remained loaded throughout the copied-colony assignment and save/reload checks. mulld does not patch combat, tending or hauling job execution.
- **Solo colony:** the copied map has one managed worker. The harness now exercises an editable worker explicitly rather than relying on a second colonist or letting an empty assertion pass.

## Existing third-party messages

The native startup output reports a ReShade library-load failure, also present in the baseline before mulld loads. The final game log contains existing warnings about Living Training Targets dependency metadata, Walls Are Solid/Combat Extended damage settings, translation data and two static-constructor attributes. These external mods/settings were not modified.

One earlier random quicktest world also produced a Rimesis player-faction lookup error during faction generation, before the mulld test began. A later full-list fresh-colony run and the final copied-colony run completed without that game error.

## Reproduce

Build the diagnostic assembly and install it, then use:

```powershell
pwsh -NoProfile -File ./tools/Smoke-Test.ps1 -CurrentModList -ProfileName compat-check -SavePath "C:\Path\To\Colony.rws"
```

Configuration and save files are copied into `artifacts/compat-check`; the normal profile is not edited. The test mutates the disposable colony, saves/reloads it, captures the UI and exits. Rebuild without `-WithSmokeTests` and reinstall the release afterward.

The baseline probe is a separate diagnostic-only assembly, built with `tools/Build.ps1 -ProbeOnly`. Its temporary installation is removed after validation. It is not included in the release.

The full final result list is in `compatibility-results.txt`.
