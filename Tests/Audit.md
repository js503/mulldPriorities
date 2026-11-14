# 0.2.1 work-assignment audit

The final five-pawn and solo save copies are tested with all 97 requested mod entries loaded; final assertion totals are recorded below. The audit exercises priority planning, real SetPriority calls, Off and undo for all 25 visible work types in both legacy and balanced modes. This verifies assignment integration, not that every work giver can finish a job: bills, tools, paths, schedules and permissions remain game/mod responsibilities.

## Bugs reproduced and fixed

- A locked fallback or disqualified worker could consume a qualified staffing slot. Regression tests now require another qualified primary/backup.
- Main-role overflow used the backup priority and could become 4 despite the promised support priority 3.
- The idle-generalist pass overrode explicit priority-3 hauling rules. The first full audit reproduced this on Balanced Hauling; the corrected run passes. Promotion is now limited to Everyone-at-4 general work, never Count/Percent, support-only or Off rules.
- Utility work consumed the legacy planner's specialist budget even though the balanced planner and UI excluded it.
- Switching from checkbox mode could reject a valid draft after numbered priorities revealed different stored values. Apply now copies/rebases the approved batch and undo restores those original values without changing the caller preview.
- A pawn drafted after preview could still receive a stale rule assignment. Apply now rejects unavailable pawns for all drafts.
- Invalid balanced-policy XML could replace rules before import failed. Validation now precedes every state replacement.
- Imported fallback priorities 1/2 could exceed the primary budget. Imports reject them; loaded invalid fallback values are normalized.
- Changing the main-role limit while paused left the commitments indicator stale. It now invalidates immediately.

## Optimization

Daily automation with no active boosts returns before scanning the colony while the next run is pending. Due daily staffing also avoids food/plant scans. Plans reuse visible work definitions and hashed protection keys, and draft validation constructs worker membership once per batch. No numerical speedup claim is made without a representative colony benchmark.

Standalone policy tests: 43 checks, including 200 seeded scenarios across 1–20 pawns, 12 roles, variable caps, protected assignments and eligibility. Scenarios verify main-role budgets, essential coverage, valid priorities and stable results when input rule order changes.

Five-pawn assertions are in audit-colony-results.txt. Single-pawn assertions are in audit-solo-results.txt. Tests also exercise checkbox-mode consent/undo, utility budgets, preset versions 1/2 and pending-daily snapshot reuse. Original saves/configuration are hash-checked and are never used as output destinations. Final release excludes diagnostic types.

## GitHub

Local origin is configured for https://github.com/js503/mulldPriorities.git. The connected GitHub account is js503, but the connector has no repository-create operation, the local Git credential helper has no noninteractive GitHub login, and Windows Firefox browser access is blocked by the tool's URL-policy support. No remote publication is claimed. A Git bundle preserves the complete local history for transfer.


## Final results

- Five-pawn colony: **277 in-game checks passed**.
- Solo colony: **278 in-game checks passed**.
- Both profiles loaded all 97 requested mod entries and exercised all 25 visible work types; no work type was skipped for missing eligible workers.
- Both final profiles completed with no managed game errors. Existing external native ReShade/shutdown messages remain.
- Original ModsConfig.xml, Test.rws and Autosave-1.rws hashes match pre-test values.
- Release version 0.2.1 is rebuilt without diagnostic types, installed in RimWorld/Mods/mulldPriorities and checked against the local DLL hash.
- GitHub publishing helper syntax validated locally. Its network publication path remains unexecuted because authentication/repository creation is unavailable in this session.
