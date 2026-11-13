# Changelog

## 0.2.1 — 2026-11-13

- Do not let locked low-priority or disqualified workers consume qualified primary/backup staffing slots.
- Keep essential overflow at support priority 3, even when backups use 4.
- Preserve explicit support-only and count-limited hauling/cleaning rules instead of promoting workers to primary duties.
- Apply utility exemptions consistently in both planners and stabilize tied work-rule ordering.
- Reject drafts if a pawn becomes unavailable after preview; reject null changes safely.
- Apply checkbox-mode drafts after numbered-priority consent without losing hidden original values; keep undo and the caller preview intact.
- Validate the balanced-policy flag before replacing imported rules. Reject primary-priority fallbacks that bypass the role budget.
- Refresh the commitments indicator immediately when its role budget changes.
- Avoid map scans while daily assignment is waiting; reuse work lists, protection sets and worker membership during planning/validation.
- Audit apply/undo/Off for all 25 visible work types in both planning modes, plus 200 seeded colony-policy scenarios.

## 0.2.0 — 2026-11-13

- Attach the needs panel to the vanilla Work tab with an expand/collapse toggle; retain native and modded work columns.
- Replace the assignment-review window with gold current-to-proposed cells in the work grid. Review does not mutate live priorities.
- Rework Balanced planning around essential coverage, distributed primary roles, separate backups and low-priority fallback work.
- Preserve manual hazardous/unknown work, locks, exclusions and existing custom-rule behavior.
- Add backup/fallback configuration and backward-compatible preset format 2.
- Add Harmony dependency for native-grid review rendering.
- Validate the integrated interface and balanced apply in a copied five-pawn colony with the current mod list.

## 0.1.1 — 2026-11-13

- Detect AutoPriorities' per-map timed assignments and pause mulld daily rules while that timer is active. Manual review/apply remains available; other mods' settings are not changed.
- Show the active competing timer in the interface and explain how to enable mulld daily management.
- Add full-current-mod-list and copied-save validation profiles, startup output capture and an independent baseline probe.
- Extend in-game checks to cover single-worker colonies, AutoPriorities overlap and food in an sbz/Adaptive Storage fridge.
- Name release packages from the compiled assembly version.

## 0.1.0 — 2026-11-13

- Combined colony-needs indicators and work overview.
- Six evidence-based indicators, staged recommendations and temporary boosts.
- Per-colonist editing, assignment locks and exclusions.
- Skill/passion staffing rules with count, percentage, everyone and off modes.
- Rule presets, validated XML import/export and optional daily reassignment.
- Review, conflict validation, batch rollback, protected undo and per-map save persistence.
- Offline build, policy tests and isolated in-game validation harness.
