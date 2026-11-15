using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mulld.Priorities
{
    // Retain the game's Work table, columns, scrolling and other mods' cell workers.
    public sealed class MainTabWindow_Priorities : MainTabWindow_Work
    {
        private readonly Dictionary<(Pawn, WorkTypeDef), Assignment> draft = new Dictionary<(Pawn, WorkTypeDef), Assignment>();
        private static readonly string[] pages = { "Rules", "Colonists", "Settings" };
        private readonly ColonyNeed[] needCards = new ColonyNeed[3];
        private int reviewPawnCount;
        private Map boundMap;
        private string selected = "food", page = "Rules", status = "Choose a preset or edit rules, then Preview Changes.";
        private Assignment inspected;
        private string inspectionText;
        private readonly Dictionary<(Pawn, WorkTypeDef), string> previewTips = new();
        private Vector2 scroll;
        private int rulePage;
        private static readonly float[] ledgerEdges = { 0, .20f, .26f, .46f, .57f, .73f, .88f, 1 };
        // All button bands share the same outer edges; only interior columns have gutters.
        internal static Rect ButtonColumn(Rect area, int index, int count)
        {
            float unit = (area.width + 8) / count;
            float left = index == 0 ? area.x : area.x + Mathf.Round(index * unit);
            float right = index == count - 1 ? area.xMax : area.x + Mathf.Round((index + 1) * unit) - 8;
            return new Rect(left, area.y, right - left, area.height);
        }
        internal static Rect LedgerCell(Rect area, int column, float y)
        {
            float left = column == 0 ? area.x : area.x + Mathf.Round(area.width * ledgerEdges[column]) + 4;
            float right = column == ledgerEdges.Length - 2 ? area.xMax : area.x + Mathf.Round(area.width * ledgerEdges[column + 1]) - 4;
            return new Rect(left, y, right - left, 25);
        }
        private static readonly string[] ledgerHeadings = { "Work", "Auto", "Primary workers", "Priority", "Backups", "Others", "Options" };
        internal static int LedgerCapacity(float height) => Math.Max(1, (int)((height - 51) / 26));
        private bool drawingNative, resizePending, review, enableNumbered;
        public bool Expanded = true;
        internal static MainTabWindow_Priorities Drawing;
        internal bool Reviewing => Expanded && review;
        private PriorityMap State => Find.CurrentMap?.GetComponent<PriorityMap>();
        private static readonly Color Gold = new Color(.86f, .71f, .44f);
        private static readonly Color Muted = new Color(.72f, .76f, .72f);
        private float PanelHeight => !Expanded ? 0 : Math.Min(560, Math.Max(400, UI.screenHeight - 300));
        protected override float ExtraTopSpace => base.ExtraTopSpace + (drawingNative ? 0 : PanelHeight);
        public override void PostOpen() { Bind(); base.PostOpen(); }
        private void Bind()
        {
            if (boundMap == Find.CurrentMap) return;
            boundMap = Find.CurrentMap; ClearDraft(); scroll = Vector2.zero;
            status = "Work changes require review and approval.";
        }
        public override void DoWindowContents(Rect rect)
        {
            Bind();
            var font = Text.Font; var anchor = Text.Anchor; var color = GUI.color; bool wrap = Text.WordWrap;
            try
            {
                float height = PanelHeight;
                if (Expanded && State != null) DrawPanel(new Rect(0, 0, rect.width, height));
                Widgets.BeginGroup(new Rect(0, height, rect.width, Math.Max(30, rect.height - height)));
                try
                {
                    drawingNative = true; Drawing = this;
                    base.DoWindowContents(new Rect(0, 0, rect.width, Math.Max(30, rect.height - height)));
                    Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; GUI.color = Color.white;
                    var toggle = new Rect(rect.width - 185, 4, 180, 28);
                    bool clicked = Widgets.ButtonText(new Rect(toggle.x, toggle.y, toggle.width - 30, toggle.height), "mulld Priorities");
                    clicked |= Widgets.ButtonText(new Rect(toggle.xMax - 30, toggle.y, 30, toggle.height), Expanded ? "▴" : "▾");
                    if (clicked) ToggleExpanded();
                }
                finally { Drawing = null; drawingNative = false; Widgets.EndGroup(); }
            }
            finally { Text.Font = font; Text.Anchor = anchor; GUI.color = color; Text.WordWrap = wrap; }
            if (resizePending) { resizePending = false; base.Notify_ResolutionChanged(); SetInitialSizeAndPosition(); }
        }
        private static void Label(Rect rect, string text, Color? tint = null)
        {
            var old = GUI.color; if (tint.HasValue) GUI.color = tint.Value;
            Widgets.LabelEllipses(rect, text ?? ""); if (!string.IsNullOrEmpty(text)) TooltipHandler.TipRegion(rect, text);
            GUI.color = old;
        }
        private void DrawPanel(Rect area)
        {
            using var measurement = Event.current.type == EventType.Repaint ? PerformanceMetrics.Measure(PerformanceOperation.PanelDraw) : default;
            Text.Font = GameFont.Small; GUI.color = Color.white; Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Medium; Label(new Rect(5, 0, area.width - 10, 27), "mulld Priorities"); Text.Font = GameFont.Small;
            string manager = Compatibility.AutomaticManager(boundMap);
            string mode = manager != null ? manager + " timer active; mulld daily assignment paused" : State.Daily ? "Daily assignment enabled" : "Manual approval";
            int workers = State.WorkerCount;
            Label(new Rect(area.width * .55f, 6, area.width * .45f - 5, 21), workers + (workers == 1 ? " colonist · " : " colonists · ") + mode, Muted);
            DrawNeeds(new Rect(5, 34, area.width - 10, 101));
            for (int i = 0; i < pages.Length; i++)
                if (Widgets.ButtonText(ButtonColumn(new Rect(5, 142, area.width - 10, 27), i, 3), page == pages[i] ? "[" + pages[i] + "]" : pages[i])) { page = pages[i]; scroll = Vector2.zero; }
            DrawPresetControl(new Rect(5, 176, area.width - 10, 28));
            var body = new Rect(5, 211, area.width - 10, Math.Max(60, area.height - 301));
            if (page == "Rules") DrawRules(body);
            else if (page == "Colonists") DrawColonists(body);
            else DrawSettings(body);
            DrawFooter(new Rect(5, area.height - 88, area.width - 10, 82));
            Widgets.DrawBoxSolid(new Rect(0, area.height - 2, area.width, 2), Gold);
        }
        private void DrawPresetControl(Rect area)
        {
            string name = (State.PresetName ?? "Custom") + (State.PresetDirty ? " *" : "");
            var preset = ButtonColumn(area, 0, 2);
            if (Widgets.ButtonText(preset, "")) OpenPresets();
            var anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Label(new Rect(preset.x + 28, preset.y, preset.width - 56, preset.height), name);
            Label(new Rect(preset.xMax - 28, preset.y, 24, preset.height), "▾");
            Text.Anchor = anchor;
            if (Widgets.ButtonText(ButtonColumn(area, 1, 2), "Save Preset…"))
                Find.WindowStack.Add(new PresetSaveDialog(State.PresetName, CaptureSaveAction()));
        }
        private void OpenPresets()
        {
            var options = PresetCatalog.Names.Select(name => new FloatMenuOption(name, () => LoadPreset(name))).ToList();
            options.Add(new FloatMenuOption("Factory defaults…", () => Find.WindowStack.Add(new FloatMenu(PresetCatalog.BuiltIns.Select(name => new FloatMenuOption(name + " (factory)", () => LoadPreset(name, true))).ToList()))));
            if (PresetCatalog.Store.Error != null) status = PresetCatalog.Store.Error;
            Find.WindowStack.Add(new FloatMenu(options));
        }
        private string LoadPreset(string name, bool factory = false)
        {
            string error = PresetCatalog.Load(State, name, factory);
            if (error != null) { status = error; return error; }
            ClearDraft();
            status = "Loaded “" + State.PresetName + "” rules. Priorities unchanged; Preview Changes to review.";
            return null;
        }
        private string SavePreset(string name, bool overwrite)
        {
            if (State == null || boundMap != Find.CurrentMap) return "The displayed colony changed. Reopen Save Rules As.";
            string error = PresetCatalog.Save(State, name, overwrite);
            status = error ?? "Saved “" + State.PresetName + "”. Priorities unchanged.";
            return error;
        }
        private Func<string, bool, string> CaptureSaveAction()
        {
            var state = State; var map = boundMap; var definition = Presets.Capture(state);
            return (name, overwrite) =>
            {
                if (Find.CurrentMap != map || State != state) return "The displayed colony changed. Reopen Save Rules As.";
                if (!System.Xml.Linq.XNode.DeepEquals(definition, Presets.Capture(state))) return "Rules changed while saving. Reopen Save Rules As.";
                return SavePreset(name, overwrite);
            };
        }
        private void DrawNeeds(Rect area)
        {
            var snapshot = State.Snapshot();
            needCards[0] = snapshot.Needs[0]; needCards[1] = snapshot.Needs[1]; needCards[2] = snapshot.Needs[2];
            var active = snapshot.Needs.Find(n => n.Key == selected) ?? needCards[0];
            if (active.Key is not ("food" or "care" or "harvest")) needCards[2] = active;
            for (int i = 0; i < needCards.Length; i++)
            {
                var need = needCards[i]; var row = new Rect(area.x, area.y + i * 24, area.width, 23);
                Widgets.DrawBoxSolid(row, new Color(1, 1, 1, i % 2 == 0 ? .055f : .025f));
                Label(new Rect(row.x + 8, row.y + 2, row.width * .22f - 16, 23), need.Label);
                Label(new Rect(row.x + row.width * .22f, row.y + 2, row.width * .18f, 23), need.Value, need.Severity > 1 ? Gold : Color.white);
                Label(new Rect(row.x + row.width * .40f, row.y + 2, row.width * .60f - 8, 23), need.Status, Muted);
                TooltipHandler.TipRegion(row, need.Evidence);
                if (Widgets.ButtonInvisible(row)) selected = need.Key;
            }
            Label(new Rect(area.x + 4, area.y + 76, area.width - 210, 23), active.Resolved ? active.Status : active.Explanation, Muted);
            var suggestion = active.Suggestion;
            if (Widgets.ButtonText(new Rect(area.xMax - 190, area.y + 75, 190, 25), active.Resolved ? "Target met" : suggestion == null ? "No change suggested" : "Preview " + suggestion.Pawn.LabelShort + " boost", active: suggestion != null))
            { Stage(suggestion.Copy()); BeginReview(); }
        }
        private void DrawFooter(Rect area)
        {
            Widgets.DrawBoxSolid(new Rect(area.x, area.y, area.width, 1), Gold);
            string heading = review ? "Pending Changes — " + reviewPawnCount + (reviewPawnCount == 1 ? " colonist · " : " colonists · ") + draft.Count + " changes · Not applied" : "Change Review — no pending changes";
            Label(new Rect(area.x, area.y + 4, area.width, 23), heading);
            if (review && !Current.Game.playSettings.useWorkPriorities)
                Widgets.CheckboxLabeled(new Rect(area.x, area.y + 28, area.width, 23), "Enable numbered work priorities for this save when applying", ref enableNumbered);
            else Label(new Rect(area.x, area.y + 28, area.width, 23), review ? inspectionText ?? "Select a gold cell to see its current → proposed priority." : status, review ? Gold : Muted);
            var actions = new Rect(area.x, area.y + 51, area.width, 28);
            if (Widgets.ButtonText(ButtonColumn(actions, 0, 4), "Preview Changes")) { StageRules(); BeginReview(); }
            if (Widgets.ButtonText(ButtonColumn(actions, 1, 4), "Discard Changes", active: draft.Count > 0)) { ClearDraft(); status = "Draft discarded; priorities unchanged."; }
            if (Widgets.ButtonText(ButtonColumn(actions, 2, 4), "Undo Last Apply", active: State.Undo.Count > 0 && draft.Count == 0)) status = State.UndoLast();
            if (Widgets.ButtonText(ButtonColumn(actions, 3, 4), draft.Count == 0 ? "Apply Changes" : "Apply " + draft.Count + " Changes", active: review && draft.Count > 0 && (Current.Game.playSettings.useWorkPriorities || enableNumbered)))
            {
                string error = ApplyDraft(enableNumbered);
                status = error ?? "Priorities applied. Previous priorities saved for undo.";
            }
        }
        private string ApplyDraft(bool enableManual)
        {
            string error = State.Apply(draft.Values.ToList(), enableManual);
            if (error == null) ClearDraft();
            return error;
        }
        private void ClearDraft()
        {
            draft.Clear(); previewTips.Clear(); inspected = null; inspectionText = null; review = false; reviewPawnCount = 0;
        }
        private void ToggleExpanded() { Expanded = !Expanded; resizePending = true; }
        private void RulesChanged()
        {
            State.PresetDirty = true; ClearDraft(); State.Invalidate();
            status = "Rules changed. Preview Changes to recalculate.";
        }
        internal void Inspect(Assignment change)
        {
            inspected = change;
            inspectionText = change.Pawn.LabelShort + " · " + WorkAccess.Label(change.Work) + ": " + WorkAccess.PriorityLabel(change.Before) + " → " + WorkAccess.PriorityLabel(change.After);
        }
        internal bool IsInspected(Assignment change) => inspected == change;
        internal string PreviewTip(Assignment change)
        {
            var key = (change.Pawn, change.Work);
            if (!previewTips.TryGetValue(key, out string tip))
                previewTips[key] = tip = change.Pawn.LabelShort + " · " + WorkAccess.Label(change.Work) + ": " + WorkAccess.PriorityLabel(change.Before) + " → " + WorkAccess.PriorityLabel(change.After) + "\n" + change.Reason + "\nProposed only; priorities remain unchanged until Apply.";
            return tip;
        }
        private void BeginReview()
        {
            review = draft.Count > 0; enableNumbered = false; resizePending = true;
            reviewPawnCount = draft.Values.Select(c => c.Pawn).Distinct().Count();
            previewTips.Clear(); inspected = null; inspectionText = null;
            if (review) Inspect(draft.Values.First());
            status = review ? "Preview ready; priorities unchanged until Apply." : "No priority changes are needed for these rules.";
        }
        private void StageRules()
        {
            foreach (var key in draft.Where(kv => kv.Value.Reason.StartsWith("Staffing rule:", StringComparison.Ordinal)).Select(kv => kv.Key).ToList()) draft.Remove(key);
            foreach (var change in State.Plan()) if (!draft.ContainsKey((change.Pawn, change.Work))) Stage(change);
        }
        private void Stage(Assignment change)
        {
            var key = (change.Pawn, change.Work);
            previewTips.Remove(key);
            if (draft.TryGetValue(key, out var previous)) change.Before = previous.Before;
            if (change.Before == change.After) draft.Remove(key); else draft[key] = change;
        }
        private int PreviewPriority(Pawn pawn, WorkTypeDef work) => draft.TryGetValue((pawn, work), out var c) ? c.After : pawn.workSettings.GetPriority(work);
        internal bool TryPreview(Pawn pawn, WorkTypeDef work, out Assignment change) => draft.TryGetValue((pawn, work), out change);
        private static void Menu(IEnumerable<string> items, Action<string> setter) => Find.WindowStack.Add(new FloatMenu(items.Select(item => new FloatMenuOption(item, () => setter(item))).ToList()));
        private void DrawRules(Rect area)
        {
            var works = WorkAccess.Works().OrderByDescending(w => w.naturalPriority).ToList();
            int capacity = LedgerCapacity(area.height);
            int lastPage = Math.Max(0, (works.Count - 1) / capacity);
            rulePage = Math.Min(rulePage, lastPage);
            Rect Cell(int column, float y) => LedgerCell(area, column, y);
            for (int c = 0; c < ledgerHeadings.Length; c++) Label(Cell(c, area.y), ledgerHeadings[c], Muted);
            int start = rulePage * capacity, end = Math.Min(works.Count, start + capacity);
            for (int i = start; i < end; i++)
            {
                var work = works[i]; var r = State.Rule(work); float y = area.y + 26 + (i - start) * 26;
                if ((i - start) % 2 == 0) Widgets.DrawBoxSolid(new Rect(area.x, y, area.width, 26), new Color(1, 1, 1, .035f));
                Label(Cell(0, y), WorkAccess.Label(work));
                bool enabled = r.Enabled;
                var autoCell = Cell(1, y);
                Widgets.CheckboxLabeled(new Rect(autoCell.center.x - 12, y, 24, 25), "", ref enabled);
                TooltipHandler.TipRegion(Cell(1, y), "Include this work type when previewing or automatically applying saved rules.");
                if (enabled != r.Enabled) { r.Enabled = enabled; RulesChanged(); }
                string staffing = r.Mode == "Count" ? "Best " + r.Amount : r.Mode == "Percent" ? r.Amount + "%" : r.Mode;
                if (Widgets.ButtonText(Cell(2, y), staffing))
                    Menu(new[] { "Everyone", "Off", "Best 1", "Best 2", "Best 3", "25%", "50%", "75%" }, v => {
                        if (v.StartsWith("Best ")) { r.Mode = "Count"; r.Amount = int.Parse(v.Substring(5)); }
                        else if (v.EndsWith("%")) { r.Mode = "Percent"; r.Amount = int.Parse(v.TrimEnd('%')); }
                        else r.Mode = v;
                        RulesChanged();
                    });
                if (Widgets.ButtonText(Cell(3, y), r.Priority.ToString())) Menu(new[]{"1","2","3","4"}, v=>{r.Priority=int.Parse(v);RulesChanged();});
                if (Widgets.ButtonText(Cell(4, y), r.Backups == 0 ? "None" : r.Backups + " at " + r.BackupPriority)) Menu(new[]{"0","1","2","3"},v=>{r.Backups=int.Parse(v);RulesChanged();});
                if (Widgets.ButtonText(Cell(5, y), WorkAccess.PriorityLabel(r.FallbackPriority))) Menu(new[]{"Off","3","4"},v=>{r.FallbackPriority=v=="Off"?0:int.Parse(v);RulesChanged();});
                if (Widgets.ButtonText(Cell(6, y), "Edit…")) Find.WindowStack.Add(new RuleOptionsDialog(r, RulesChanged));
            }
            float bottom = area.yMax - 25;
            Label(new Rect(area.x + 3, bottom, area.width - 180, 25), works.Count == 0 ? "No work types" : (start + 1) + "–" + end + " / " + works.Count + " rules", Muted);
            if (Widgets.ButtonText(new Rect(area.xMax - 170, bottom, 81, 25), "Previous", active: rulePage > 0)) rulePage--;
            if (Widgets.ButtonText(new Rect(area.xMax - 81, bottom, 81, 25), "Next", active: rulePage < lastPage)) rulePage++;
        }
        private void DrawColonists(Rect area)
        {
            var workers = State.Workers;
            var view = new Rect(0, 0, area.width - 20, workers.Count * 36 + 26);
            Widgets.BeginScrollView(area, ref scroll, view);
            try
            {
                float y = 0;
                foreach (var pawn in workers)
                {
                    if (y + 36 < scroll.y || y > scroll.y + area.height) { y += 36; continue; }
                    bool managed = State.Managed(pawn), before = managed;
                    Widgets.CheckboxLabeled(new Rect(5,y,view.width*.45f,28),pawn.LabelShort + (WorkAccess.Available(pawn)?"":" · unavailable"),ref managed);
                    if (managed != before) { State.Excluded.Remove(pawn.ThingID); if(!managed)State.Excluded.Add(pawn.ThingID); ClearDraft(); State.Invalidate(); status="Colonist selection changed. Preview Changes to recalculate."; }
                    Label(new Rect(view.width*.48f,y+2,view.width*.25f,25),State.MainJobs(pawn)+" primary jobs",Muted);
                    if(Widgets.ButtonText(new Rect(view.width*.76f,y,view.width*.23f,27),"Assignment locks"))
                        Find.WindowStack.Add(new FloatMenu(WorkAccess.Works().Where(w=>WorkAccess.Capable(pawn,w)).Select(w=>new FloatMenuOption((State.IsLocked(pawn,w)?"Unlock ":"Lock ")+WorkAccess.Label(w),()=>{State.SetLock(pawn,w,!State.IsLocked(pawn,w));ClearDraft();status="Locks changed. Preview Changes to recalculate.";})).ToList()));
                    y += 36;
                }
            }
            finally { Widgets.EndScrollView(); }
        }
        private void DrawSettings(Rect area)
        {
            var view = new Rect(0,0,area.width-20,420);
            Widgets.BeginScrollView(area,ref scroll,view);
            try
            {
                var list=new Listing_Standard();list.Begin(view);
                bool daily=State.Daily;
                list.CheckboxLabeled("Reassign saved rules once per in-game day",ref daily);
                if(daily!=State.Daily)
                {
                    if(daily&&Compatibility.AutomaticManager(boundMap)!=null)status="Disable AutoPriorities' timer on this map before enabling mulld daily rules.";
                    else if(daily&&!Current.Game.playSettings.useWorkPriorities)status="Enable numbered priorities using the normal Work checkbox first.";
                    else{State.Daily=daily;State.NextDailyTick=Find.TickManager.TicksGame+60000;}
                }
                bool balanced=State.BalancedPolicy;
                list.CheckboxLabeled("Use primary / backup / other-worker rules",ref balanced);
                if(balanced!=State.BalancedPolicy){State.BalancedPolicy=balanced;RulesChanged();}
                list.Label("Primary jobs per colonist: "+State.MaxMainJobs+" (essential overflow stays enabled at priority 3)");
                int mainJobs=(int)Math.Round(list.Slider(State.MaxMainJobs,1,10));
                if(mainJobs!=State.MaxMainJobs){State.MaxMainJobs=mainJobs;RulesChanged();}
                list.Label("Food reserve target: "+State.FoodTarget.ToString("0.0")+" days");
                float target=(float)Math.Round(list.Slider(State.FoodTarget,.5f,10f)*2)/2;
                if(Math.Abs(target-State.FoodTarget)>.01){State.FoodTarget=target;State.Invalidate();}
                if(list.ButtonText("All colony needs"))Find.WindowStack.Add(new FloatMenu(State.Snapshot().Needs.Select(n=>new FloatMenuOption(n.Label+": "+n.Value,()=>{selected=n.Key;})).ToList()));
                if(list.ButtonText("Export staffing rules"))status=Presets.Export(State);
                if(list.ButtonText("Import staffing rules")){status=Presets.Import(State);if(status.StartsWith("Imported",StringComparison.Ordinal))ClearDraft();}
                list.Label("Locks, exclusions and active boosts are protected. Unknown mod work, hunting and dark study remain manual in the balanced preset. Indicators are estimates; bills, storage and access may block jobs.");
                list.End();
            }
            finally { Widgets.EndScrollView(); }
        }
#if MULLD_SMOKE_TESTS
        internal void SmokeView(string tab, WorkTypeDef work = null, bool preview = false)
        {
            Expanded=true;page=pages.Contains(tab)?tab:"Rules";scroll=Vector2.zero;review=false;
            if(preview)
            {
                Presets.Balanced(State);StageRules();
                if(draft.Count==0){var p=State.Workers.First(p=>State.Managed(p)&&WorkAccess.Capable(p,work)&&!State.IsLocked(p,work));int before=p.workSettings.GetPriority(work);Stage(new Assignment(p,work,before,before==3?4:3,"Smoke review"));}
                BeginReview();
            }
            resizePending=true;
        }
        internal void SmokeLastRulePage() => rulePage = int.MaxValue;
        internal void SmokeRuleOptions(WorkTypeDef work) => Find.WindowStack.Add(new RuleOptionsDialog(State.Rule(work), RulesChanged));
        internal int SmokeDraftCount => draft.Count;
        internal void SmokeCollapse() { if(Expanded)ToggleExpanded(); }
        internal string SmokeApply()
        {
            string error=ApplyDraft(true);
            if(error==null)status="Priorities applied; undo available.";
            return error;
        }
        internal string SmokeLoadPreset(string name)=>LoadPreset(name);
        internal string SmokeSavePreset(string name,bool overwrite)=>SavePreset(name,overwrite);
        internal void SmokePreview(){StageRules();BeginReview();}
        internal Assignment SmokeFirstChange=>draft.Values.FirstOrDefault();
        internal void SmokeInspect(Assignment change)=>Inspect(change);
        internal string SmokeInspection=>inspectionText;
        internal void SmokeToggle()=>ToggleExpanded();
        internal void SmokeRuleChanged()=>RulesChanged();
        internal Func<string,bool,string> SmokeSaveAction()=>CaptureSaveAction();
#endif
    }
}
