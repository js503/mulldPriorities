using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mulld.Priorities
{
    public sealed class MainTabWindow_Priorities : MainTabWindow
    {
        private readonly Dictionary<string, Assignment> draft = new Dictionary<string, Assignment>();
        private Map boundMap;
        private string selected = "food", page = "Work", status = "Select a colony need or edit work assignments.";
        private WorkTypeDef expanded;
        private Vector2 scroll;
        private bool review, enableNumbered;
        private List<Assignment> reviewPlan = new List<Assignment>();
        private static readonly Color Gold = new Color(0.82f, 0.68f, 0.43f);
        private static readonly Color Muted = new Color(0.72f, 0.76f, 0.73f);
        public override Vector2 RequestedTabSize => new Vector2(Math.Min(1180f, UI.screenWidth - 30f), Math.Min(850f, UI.screenHeight - 65f));
        public override void PostOpen() { base.PostOpen(); Bind(); }
        private PriorityMap State => Find.CurrentMap?.GetComponent<PriorityMap>();
        private void Bind()
        {
            if (boundMap == Find.CurrentMap) return;
            boundMap = Find.CurrentMap;
            draft.Clear(); review = false; expanded = null; scroll = Vector2.zero;
            status = "Changes apply only to the displayed map.";
        }
        public override void DoWindowContents(Rect rect)
        {
            Bind();
            if (State == null) { Widgets.Label(rect, "Open a colony map to manage work."); return; }
            var font = Text.Font; var color = GUI.color; var anchor = Text.Anchor; var wrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Medium;
                Label(new Rect(0, 0, rect.width - 210, 34), "mulld Priorities");
                Text.Font = GameFont.Small;
                Label(new Rect(0, 35, rect.width - 210, 26), State.Workers.Count + " workers · " + (State.Daily ? "Daily rule assignment" : "Manual approval") + " · " + boundMap.Parent.LabelCap);
                if (Widgets.ButtonText(new Rect(rect.width - 195, 4, 95, 30), "Refresh")) { State.Snapshot(true); status = "Colony indicators refreshed; drafts preserved."; }
                if (Widgets.ButtonText(new Rect(rect.width - 94, 4, 94, 30), "All needs")) NeedMenu();
                DrawNeeds(new Rect(0, 70, rect.width, 174));
                float x = 0;
                foreach (string tab in new[] { "Work", "Colonists", "Settings" })
                {
                    if (Widgets.ButtonText(new Rect(x, 255, 105, 30), page == tab ? "[ " + tab + " ]" : tab)) { page = tab; scroll = Vector2.zero; }
                    x += 112;
                }
                Rect body = new Rect(0, 298, rect.width, Math.Max(60, rect.height - 376));
                if (review) DrawReview(body);
                else if (page == "Colonists") DrawColonists(body);
                else if (page == "Settings") DrawSettings(body);
                else DrawWork(body);
                DrawFooter(new Rect(0, rect.height - 66, rect.width, 66));
            }
            finally { Text.Font = font; GUI.color = color; Text.Anchor = anchor; Text.WordWrap = wrap; }
        }
        private static void Label(Rect rect, string text, Color? color = null)
        {
            var old = GUI.color; if (color.HasValue) GUI.color = color.Value;
            Widgets.LabelEllipses(rect, text ?? "");
            if (!string.IsNullOrEmpty(text)) TooltipHandler.TipRegion(rect, text);
            GUI.color = old;
        }
        private void NeedMenu()
        {
            var menu = State.Snapshot().Needs.Select(n => new FloatMenuOption(n.Label + ": " + n.Value, () => { selected = n.Key; review = false; })).ToList();
            Find.WindowStack.Add(new FloatMenu(menu));
        }
        private void DrawNeeds(Rect area)
        {
            var snapshot = State.Snapshot();
            var choices = snapshot.Needs.Where(n => n.Key is "food" or "care" or "harvest").ToList();
            var active = snapshot.Needs.Find(n => n.Key == selected) ?? choices[0];
            if (!choices.Contains(active)) choices[2] = active;
            for (int i = 0; i < choices.Count; i++)
            {
                var need = choices[i];
                var box = new Rect(area.x + i * (area.width + 10) / 3, area.y, (area.width - 20) / 3, 78);
                Widgets.DrawMenuSection(box);
                if (need.Key == selected) Widgets.DrawBoxSolid(new Rect(box.x, box.y, 3, box.height), Gold);
                Label(new Rect(box.x + 12, box.y + 5, box.width - 24, 22), need.Label, need.Severity > 1 ? Gold : Muted);
                Text.Font = GameFont.Medium; Label(new Rect(box.x + 12, box.y + 25, box.width - 24, 30), need.Value); Text.Font = GameFont.Small;
                Label(new Rect(box.x + 12, box.y + 54, box.width - 24, 20), need.Status, Muted);
                if (Widgets.ButtonInvisible(box)) { selected = need.Key; review = false; }
            }
            var detail = new Rect(area.x, area.y + 88, area.width, 86);
            Widgets.DrawMenuSection(detail);
            float actionWidth = 185;
            Label(new Rect(detail.x + 10, detail.y + 4, detail.width - actionWidth - 30, 25), active.Label + " — " + active.Explanation);
            Label(new Rect(detail.x + 10, detail.y + 30, detail.width - actionWidth - 30, 22), active.Evidence, Muted);
            var suggestion = active.Suggestion;
            string caption = suggestion == null ? "No safe change found" : "Stage " + suggestion.Pawn.LabelShort + ": " + suggestion.After;
            if (Widgets.ButtonText(new Rect(detail.xMax - actionWidth - 10, detail.y + 12, actionWidth, 32), caption, active: suggestion != null))
            {
                Stage(suggestion.Copy()); expanded = suggestion.Work; page = "Work"; review = false;
                status = "Temporary boost staged. Other work may slow down; review the competing assignments.";
            }
            string protection = suggestion == null ? "Check bills, access, existing priorities, exclusions and locks." : "Restore when need resolves, or after " + ((suggestion.ExpiresAt - snapshot.Tick) / 2500f).ToString("0") + " in-game hours. Other work may slow down.";
            Label(new Rect(detail.x + 10, detail.y + 57, detail.width - 20, 22), protection, Muted);
        }
        private void DrawWork(Rect area)
        {
            var toolbar = new Rect(area.x, area.y, area.width, 32);
            if (Widgets.ButtonText(new Rect(toolbar.x, toolbar.y, 150, 28), "Colony preset"))
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption> {
                    new FloatMenuOption("Balanced colony (sets rules only)", () => { Presets.Balanced(State); status = "Balanced rules selected. Stage rules to preview assignments."; }),
                    new FloatMenuOption("Export rules", () => { status = Presets.Export(State); }),
                    new FloatMenuOption("Import rules", () => { status = Presets.Import(State); }) }));
            if (Widgets.ButtonText(new Rect(toolbar.x + 160, toolbar.y, 160, 28), "Stage rule assignments"))
            {
                int count = 0;
                foreach (var key in draft.Where(kv => kv.Value.Reason.StartsWith("Staffing rule:", StringComparison.Ordinal)).Select(kv => kv.Key).ToList()) draft.Remove(key);
                foreach (var change in State.Plan()) { if (!draft.ContainsKey(change.Key)) { Stage(change); count++; } }
                status = count + " rule changes staged; other manual draft edits preserved.";
            }
            Label(new Rect(toolbar.x + 332, toolbar.y + 3, Math.Max(0, toolbar.width - 332), 24), "1 = first · 4 = spare time · Off = unassigned", Muted);
            var works = WorkAccess.Works().OrderByDescending(w => w.naturalPriority).ToList();
            var workers = State.Workers;
            float expandedHeight = Math.Max(340, workers.Count * 33 + 90);
            var outer = new Rect(area.x, area.y + 36, area.width, area.height - 36);
            var view = new Rect(0, 0, outer.width - 20, works.Count * 64 + (expanded == null ? 0 : expandedHeight) + 28);
            Widgets.BeginScrollView(outer, ref scroll, view);
            try
            {
                float a = view.width * .19f, b = view.width * .24f, c = view.width * .43f;
                Label(new Rect(8, 0, a, 24), "Work", Muted); Label(new Rect(a, 0, b, 24), "Staffing rule", Muted); Label(new Rect(a + b, 0, c, 24), "Assigned priorities", Muted);
                float y = 28;
                var active = State.Snapshot().Needs.Find(n => n.Key == selected);
                foreach (var work in works)
                {
                    var row = new Rect(0, y, view.width, 62);
                    if (active?.WorkName == work.defName) Widgets.DrawHighlight(row);
                    Label(new Rect(8, y + 3, a - 12, 26), WorkAccess.Label(work));
                    var need = State.Snapshot().Needs.Find(n => n.WorkName == work.defName);
                    Label(new Rect(8, y + 31, a - 12, 22), need == null ? "" : need.Value, need != null && need.Severity > 1 ? Gold : Muted);
                    var rule = State.Rule(work);
                    Label(new Rect(a, y + 3, b - 8, 25), rule.Enabled ? (rule.Mode == "Count" ? "Best " + rule.Amount : rule.Mode == "Percent" ? rule.Amount + "%" : rule.Mode) + " · priority " + rule.Priority : "Manual assignments");
                    Label(new Rect(a, y + 31, b - 8, 22), rule.Enabled ? "Skill + " + (rule.FavorPassion ? "passion" : "workload") : "Rule disabled", Muted);
                    string assigned = string.Join(", ", workers.Where(p => WorkAccess.Capable(p, work) && PreviewPriority(p, work) > 0).Select(p => p.LabelShort + " " + PreviewPriority(p, work) + (draft.ContainsKey(WorkAccess.Key(p, work)) ? "*" : "") + (State.IsLocked(p, work) ? " [L]" : "")));
                    Label(new Rect(a + b, y + 3, c - 12, 47), string.IsNullOrEmpty(assigned) ? "No assignments" : assigned);
                    if (Widgets.ButtonText(new Rect(a + b + c, y + 10, view.width - a - b - c - 6, 29), expanded == work ? "Close" : "Edit")) expanded = expanded == work ? null : work;
                    Widgets.DrawLineHorizontal(0, y + 62, view.width);
                    y += 64;
                    if (expanded == work) { DrawEditor(new Rect(8, y + 6, view.width - 16, expandedHeight - 12), work, workers); y += expandedHeight; }
                }
            }
            finally { Widgets.EndScrollView(); }
        }
        private int PreviewPriority(Pawn p, WorkTypeDef work) => draft.TryGetValue(WorkAccess.Key(p, work), out var change) ? change.After : p.workSettings.GetPriority(work);
        private void Stage(Assignment assignment)
        {
            if (draft.TryGetValue(assignment.Key, out var previous)) assignment.Before = previous.Before;
            if (assignment.After == assignment.Before) draft.Remove(assignment.Key);
            else draft[assignment.Key] = assignment;
        }
        private static void Menu(IEnumerable<string> values, Action<string> setter) => Find.WindowStack.Add(new FloatMenu(values.Select(value => new FloatMenuOption(value, () => setter(value))).ToList()));
        private void DrawEditor(Rect rect, WorkTypeDef work, List<Pawn> workers)
        {
            var rule = State.Rule(work);
            float split = rect.width * .54f;
            Label(new Rect(rect.x, rect.y, split, 25), "Colonist assignments · * = staged");
            float y = rect.y + 30;
            foreach (var p in workers)
            {
                bool capable = WorkAccess.Capable(p, work), managed = State.Managed(p), locked = State.IsLocked(p, work);
                Label(new Rect(rect.x, y, split - 180, 28), p.LabelShort + (capable ? " · skill " + WorkAccess.Skill(p, work).ToString("0") : " · incapable"), managed ? (Color?)null : Muted);
                if (Widgets.ButtonText(new Rect(rect.x + split - 176, y, 80, 27), capable ? WorkAccess.PriorityLabel(PreviewPriority(p, work)) : "—", active: capable && managed && !locked))
                {
                    var pawn = p;
                    Menu(new[] { "Off", "1", "2", "3", "4" }, value => { Stage(new Assignment(pawn, work, pawn.workSettings.GetPriority(work), value == "Off" ? 0 : int.Parse(value), "Manual adjustment")); status = "Manual assignment staged."; });
                }
                if (Widgets.ButtonText(new Rect(rect.x + split - 90, y, 75, 27), locked ? "Locked" : "Lock", active: capable))
                { State.SetLock(p, work, !locked); draft.Remove(WorkAccess.Key(p, work)); status = "Lock updated. Locked assignments are protected from plans and undo."; }
                y += 33;
            }
            Label(new Rect(rect.x, y + 4, split - 15, 42), "Exclusions are in Colonists. Rules affect only eligible colonists.", Muted);
            float rx = rect.x + split + 6, width = rect.width - split - 6; y = rect.y;
            Widgets.CheckboxLabeled(new Rect(rx, y, width, 28), "Manage with a staffing rule", ref rule.Enabled); y += 34;
            if (Widgets.ButtonText(new Rect(rx, y, width, 28), "Staffing: " + rule.Mode)) Menu(new[] { "Count", "Percent", "Everyone", "Off" }, value => rule.Mode = value); y += 34;
            if (rule.Mode is "Count" or "Percent")
            {
                Label(new Rect(rx, y, width * .52f, 26), rule.Mode == "Count" ? "Colonists: " + rule.Amount : "Percent: " + rule.Amount);
                if (Widgets.ButtonText(new Rect(rx + width * .56f, y, 38, 26), "−")) rule.Amount = Math.Max(0, rule.Amount - (rule.Mode == "Percent" ? 5 : 1));
                if (Widgets.ButtonText(new Rect(rx + width * .56f + 44, y, 38, 26), "+")) rule.Amount = Math.Min(rule.Mode == "Percent" ? 100 : workers.Count, rule.Amount + (rule.Mode == "Percent" ? 5 : 1));
                y += 32;
            }
            if (Widgets.ButtonText(new Rect(rx, y, width, 27), "Rule priority: " + rule.Priority)) Menu(new[] { "1", "2", "3", "4" }, value => rule.Priority = int.Parse(value)); y += 33;
            Widgets.CheckboxLabeled(new Rect(rx, y, width, 27), "Favor passions", ref rule.FavorPassion); y += 29;
            Widgets.CheckboxLabeled(new Rect(rx, y, width, 27), "Adults only", ref rule.AdultsOnly); y += 29;
            if (Widgets.ButtonText(new Rect(rx, y, width, 27), "Minimum skill: " + rule.MinSkill)) Menu(Enumerable.Range(0, 21).Select(i => i.ToString()), value => rule.MinSkill = int.Parse(value)); y += 32;
            Label(new Rect(rx, y, width, 36), "Rules are saved immediately; work changes require review or daily automation.", Muted);
        }
        private void DrawColonists(Rect area)
        {
            var workers = State.Workers;
            var view = new Rect(0, 0, area.width - 20, workers.Count * 46 + 50);
            Widgets.BeginScrollView(area, ref scroll, view);
            try
            {
                Label(new Rect(5, 0, view.width, 30), "Uncheck a colonist to keep every assignment under manual control.");
                float y = 38;
                foreach (var p in workers)
                {
                    bool managed = State.Managed(p), before = managed;
                    Widgets.CheckboxLabeled(new Rect(8, y, view.width * .60f, 30), p.LabelShort + " · " + (WorkAccess.Available(p) ? "Available" : "Currently unavailable"), ref managed);
                    if (managed != before)
                    {
                        State.Excluded.Remove(p.ThingID); if (!managed) State.Excluded.Add(p.ThingID);
                        foreach (var key in draft.Where(kv => kv.Value.Pawn == p).Select(kv => kv.Key).ToList()) draft.Remove(key);
                        State.Invalidate();
                    }
                    Label(new Rect(view.width * .64f, y + 3, view.width * .35f, 30), State.MainJobs(p) + " main jobs · " + State.Locks.Count(k => k.StartsWith(p.ThingID + "|", StringComparison.Ordinal)) + " locks", Muted);
                    y += 46;
                }
            }
            finally { Widgets.EndScrollView(); }
        }
        private void DrawSettings(Rect area)
        {
            var view = new Rect(0, 0, area.width - 20, 440);
            Widgets.BeginScrollView(area, ref scroll, view);
            try
            {
                var list = new Listing_Standard(); list.Begin(view);
                bool daily = State.Daily;
                list.CheckboxLabeled("Reassign enabled staffing rules once per in-game day", ref daily, "This applies saved rules without a preview. It never automatically accepts colony-need recommendations.");
                if (daily != State.Daily)
                {
                    if (daily && !Current.Game.playSettings.useWorkPriorities) status = "Apply a reviewed assignment with numbered priorities enabled before switching on daily automation.";
                    else { State.Daily = daily; State.NextDailyTick = Find.TickManager.TicksGame + 60000; }
                }
                list.Label("Food reserve target: " + State.FoodTarget.ToString("0.0") + " days");
                float target = (float)Math.Round(list.Slider(State.FoodTarget, .5f, 10f) * 2f) / 2f;
                if (Math.Abs(target - State.FoodTarget) > .01f) { State.FoodTarget = target; State.Invalidate(); }
                list.Label("Maximum main jobs per colonist in staffing plans: " + State.MaxMainJobs);
                State.MaxMainJobs = (int)Math.Round(list.Slider(State.MaxMainJobs, 1, 10));
                list.Label("Main jobs are priority 1-2. Firefighting, patient and bed rest are exempt. Locks and existing unmanaged jobs count toward the cap.");
                list.Gap(12);
                list.Label("Indicators update every in-game hour while open. Suggestions are estimates: bills, pathing, food restrictions and storage can block work even when staffing looks adequate.");
                list.Gap(12);
                list.Label("Temporary boosts restore the previous assignment when the need resolves or the timer expires. Changed, locked and excluded assignments are preserved. Colonists off this map are checked when they return.");
                list.Gap(12);
                list.Label("Numbered priorities supported: Off and 1-4. Expanded-priority mods and multiplayer have not been validated. Avoid enabling two automatic work managers on the same colonists.");
                list.End();
            }
            finally { Widgets.EndScrollView(); }
        }
        private void DrawFooter(Rect area)
        {
            Widgets.DrawLineHorizontal(area.x, area.y, area.width);
            Label(new Rect(area.x, area.y + 5, Math.Max(80, area.width - 465), 23), draft.Count + " pending changes · " + State.Boosts.Count + " active boosts");
            Label(new Rect(area.x, area.y + 30, area.width, 28), status, Muted);
            float x = area.xMax - 452;
            if (Widgets.ButtonText(new Rect(x, area.y + 4, 100, 28), "Discard", active: draft.Count > 0)) { draft.Clear(); review = false; status = "Draft discarded; applied work priorities unchanged."; }
            if (Widgets.ButtonText(new Rect(x + 108, area.y + 4, 108, 28), "Undo apply", active: State.Undo.Count > 0 && draft.Count == 0)) { status = State.UndoLast(); review = false; }
            if (Widgets.ButtonText(new Rect(x + 224, area.y + 4, 228, 28), review ? "Back to editing" : "Review " + draft.Count + " changes", active: draft.Count > 0))
            {
                if (review) review = false;
                else { reviewPlan = draft.Values.OrderBy(c => c.Pawn.LabelShort).ThenBy(c => c.Work.labelShort).Select(c => c.Copy()).ToList(); review = true; scroll = Vector2.zero; enableNumbered = false; }
            }
        }
        private void DrawReview(Rect area)
        {
            float bottom = Current.Game.playSettings.useWorkPriorities ? 64 : 100;
            var outer = new Rect(area.x, area.y, area.width, Math.Max(30, area.height - bottom));
            var view = new Rect(0, 0, outer.width - 20, 35 + reviewPlan.Count * 74);
            Widgets.BeginScrollView(outer, ref scroll, view);
            try
            {
                Label(new Rect(8, 0, view.width - 16, 28), "Review assignments — other work may slow down");
                float y = 35;
                foreach (var c in reviewPlan)
                {
                    Label(new Rect(8, y, view.width - 16, 25), c.Pawn.LabelShort + " · " + WorkAccess.Label(c.Work) + ": " + WorkAccess.PriorityLabel(c.Before) + " → " + WorkAccess.PriorityLabel(c.After));
                    string timing = c.NeedKey == null ? "Manual/rule change; stays until edited or recalculated." : "Temporary boost; restores on resolution or at " + ((c.ExpiresAt - Find.TickManager.TicksGame) / 2500f).ToString("0.0") + " remaining in-game hours.";
                    Label(new Rect(8, y + 25, view.width - 16, 22), c.Reason + " · " + timing, Muted);
                    string competing = string.Join(", ", WorkAccess.Works().Where(w => w != c.Work && WorkAccess.Capable(c.Pawn, w) && PreviewPriority(c.Pawn, w) > 0 && PreviewPriority(c.Pawn, w) <= c.After).Select(w => w.labelShort));
                    Label(new Rect(8, y + 47, view.width - 16, 22), string.IsNullOrEmpty(competing) ? "No other equal/higher priority assignments." : "Equal/higher priority work remains: " + competing, Gold);
                    y += 74;
                }
            }
            finally { Widgets.EndScrollView(); }
            float by = area.yMax - bottom + 4;
            if (!Current.Game.playSettings.useWorkPriorities)
            {
                Widgets.CheckboxLabeled(new Rect(area.x + 8, by, area.width - 16, 30), "Enable numbered work priorities for this save (all maps)", ref enableNumbered); by += 34;
            }
            if (Widgets.ButtonText(new Rect(area.x + 8, by, 230, 30), "Apply reviewed changes", active: Current.Game.playSettings.useWorkPriorities || enableNumbered))
            {
                string error = State.Apply(reviewPlan, enableNumbered);
                if (error == null) { draft.Clear(); review = false; status = "Assignments applied. Indicators improve only as colonists complete work."; }
                else status = error;
            }
        }
#if MULLD_SMOKE_TESTS
        internal void SmokeView(string tab, WorkTypeDef work = null, bool preview = false)
        {
            page = tab; expanded = work; scroll = Vector2.zero; review = false;
            if (preview)
            {
                var pawn = State.Workers.First(p => WorkAccess.Capable(p, work) && State.Managed(p) && !State.IsLocked(p, work));
                int before = pawn.workSettings.GetPriority(work);
                Stage(new Assignment(pawn, work, before, before == 3 ? 4 : 3, "Smoke review"));
                reviewPlan = draft.Values.Select(c => c.Copy()).ToList(); review = true;
            }
        }
#endif
    }
    public sealed class MainButtonWorker_Priorities : MainButtonWorker_ToggleTab
    {
        public override void DoButton(Rect rect)
        {
            // The base worker capitalizes every label. Preserve the mulld brand.
            if (Widgets.ButtonText(rect, "mulld Priorities", active: !Disabled)) InterfaceTryActivate();
            if (Find.MainTabsRoot.OpenTab == def) Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 2), new Color(.82f, .68f, .43f));
            TooltipHandler.TipRegion(rect, def.description);
        }
    }
}
