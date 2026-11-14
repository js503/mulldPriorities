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
        private bool drawingNative, resizePending, review, enableNumbered;
        public bool Expanded = true;
        internal static MainTabWindow_Priorities Drawing;
        internal bool Reviewing => Expanded && review;
        private PriorityMap State => Find.CurrentMap?.GetComponent<PriorityMap>();
        private static readonly Color Gold = new Color(.86f, .71f, .44f);
        private static readonly Color Muted = new Color(.72f, .76f, .72f);
        private float PanelHeight => !Expanded ? 0 : Math.Min(440, Math.Max(400, UI.screenHeight * .50f));
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
                    var toggle = new Rect(rect.width - 184, 4, 180, 28);
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
            Label(new Rect(5, 28, area.width - 10, 21), workers + (workers == 1 ? " colonist · " : " colonists · ") + mode, Muted);
            DrawNeeds(new Rect(5, 51, area.width - 10, 78));
            Widgets.DrawBoxSolid(new Rect(0, 135, area.width, 1), Gold);
            float x = 5;
            foreach (var tab in pages)
            {
                if (Widgets.ButtonText(new Rect(x, 142, 148, 27), page == tab ? "[" + tab + "]" : tab)) { page = tab; scroll = Vector2.zero; }
                x += 156;
            }
            DrawPresetControl(new Rect(5, 176, 316, 28));
            Label(new Rect(334, 178, Math.Max(0, area.width - 339), 24), status, Muted);
            var body = new Rect(5, 211, area.width - 10, Math.Max(30, area.height - 317));
            if (page == "Rules") DrawRules(body);
            else if (page == "Colonists") DrawColonists(body);
            else DrawSettings(body);
            DrawFooter(new Rect(5, area.height - 100, area.width - 10, 94));
            Widgets.DrawBoxSolid(new Rect(0, area.height - 2, area.width, 2), Gold);
        }
        private void DrawPresetControl(Rect area)
        {
            string name = (State.PresetName ?? "Custom") + (State.PresetDirty ? " *" : "");
            var nameRect = new Rect(area.x, area.y, area.width - 96, area.height);
            bool open = Widgets.ButtonText(nameRect, "");
            var anchor = Text.Anchor; bool wrap = Text.WordWrap;
            Text.Anchor = TextAnchor.MiddleCenter; Text.WordWrap = false;
            Widgets.LabelEllipses(new Rect(nameRect.x + 6, nameRect.y, nameRect.width - 12, nameRect.height), name);
            Text.Anchor = anchor; Text.WordWrap = wrap;
            TooltipHandler.TipRegion(nameRect, name);
            open |= Widgets.ButtonText(new Rect(area.xMax - 96, area.y, 32, area.height), "▾");
            if (open) OpenPresets();
            if (IconButton(new Rect(area.xMax - 64, area.y, 32, area.height), UiAssets.Preview, "Preview Changes")) { StageRules(); BeginReview(); }
            if (IconButton(new Rect(area.xMax - 32, area.y, 32, area.height), TexButton.Save, "Save Rules As — save or overwrite a named preset"))
                Find.WindowStack.Add(new PresetSaveDialog(State.PresetName, CaptureSaveAction()));
        }
        private static bool IconButton(Rect rect, Texture2D icon, string tooltip)
        {
            bool clicked = Widgets.ButtonText(rect, "");
            Widgets.DrawTextureFitted(rect.ContractedBy(6), icon, 1f);
            TooltipHandler.TipRegion(rect, tooltip);
            return clicked;
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
            const float cardHeight = 48;
            for (int i = 0; i < needCards.Length; i++)
            {
                var need = needCards[i]; var box = new Rect(area.x + i * (area.width + 9) / 3, area.y, (area.width - 18) / 3, cardHeight);
                Widgets.DrawMenuSection(box);
                if (need.Key == selected) Widgets.DrawBoxSolid(new Rect(box.x, box.y, 3, box.height), Gold);
                Label(new Rect(box.x + 10, box.y + 1, box.width - 20, 21), need.Label, need.Severity > 1 ? Gold : Muted);
                Text.Font = GameFont.Medium; Label(new Rect(box.x + 10, box.y + 20, box.width - 20, 27), need.Value); Text.Font = GameFont.Small;
                TooltipHandler.TipRegion(box, need.Status + "\n" + need.Evidence);
                if (Widgets.ButtonInvisible(box)) selected = need.Key;
            }
            Label(new Rect(area.x + 4, area.y + 53, area.width - 210, 23), active.Resolved ? active.Status : active.Explanation, Muted);
            var suggestion = active.Suggestion;
            if (Widgets.ButtonText(new Rect(area.xMax - 195, area.y + 52, 190, 25), active.Resolved ? "Target met" : suggestion == null ? "No change suggested" : "Preview " + suggestion.Pawn.LabelShort + " boost", active: suggestion != null))
            { Stage(suggestion.Copy()); BeginReview(); }
        }
        private void DrawFooter(Rect area)
        {
            Widgets.DrawBoxSolid(new Rect(area.x, area.y, area.width, 1), Gold);
            string heading = review ? "Pending Changes — " + reviewPawnCount + (reviewPawnCount == 1 ? " colonist · " : " colonists · ") + draft.Count + " changes · Not applied" : "Change Review — no pending changes";
            Label(new Rect(area.x, area.y + 4, area.width, 23), heading);
            if (review && !Current.Game.playSettings.useWorkPriorities)
                Widgets.CheckboxLabeled(new Rect(area.x, area.y + 28, area.width, 23), "Enable numbered work priorities for this save when applying", ref enableNumbered);
            else Label(new Rect(area.x, area.y + 28, area.width, 23), review ? inspectionText ?? "Select a gold cell to see its current → proposed priority." : "Selecting or saving a preset changes rules only.", review ? Gold : Muted);
            float width = (area.width - 16) / 3, y = area.y + 57;
            if (Widgets.ButtonText(new Rect(area.x, y, width, 28), "Discard Changes", active: draft.Count > 0)) { ClearDraft(); status = "Draft discarded; priorities unchanged."; }
            if (Widgets.ButtonText(new Rect(area.x + width + 8, y, width, 28), "Undo Last Apply", active: State.Undo.Count > 0 && draft.Count == 0)) status = State.UndoLast();
            if (Widgets.ButtonText(new Rect(area.x + 2 * (width + 8), y, width, 28), draft.Count == 0 ? "Apply Changes" : "Apply " + draft.Count + " Changes", active: review && draft.Count > 0 && (Current.Game.playSettings.useWorkPriorities || enableNumbered)))
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
            var view = new Rect(0, 0, area.width - 20, works.Count * 61);
            Widgets.BeginScrollView(area, ref scroll, view);
            try
            {
                float y = 0;
                foreach (var work in works)
                {
                    if (y + 61 < scroll.y || y > scroll.y + area.height) { y += 61; continue; }
                    var r = State.Rule(work);
                    bool enabled = r.Enabled;
                    Widgets.CheckboxLabeled(new Rect(2, y, 142, 27), WorkAccess.Label(work), ref enabled);
                    if (enabled != r.Enabled) { r.Enabled = enabled; RulesChanged(); }
                    string staffing = r.Mode == "Count" ? "Best " + r.Amount : r.Mode == "Percent" ? r.Amount + "%" : r.Mode;
                    if (Widgets.ButtonText(new Rect(150, y, 170, 27), staffing))
                        Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption> { new FloatMenuOption("Everyone",()=>{r.Mode="Everyone";RulesChanged();}),new FloatMenuOption("Off",()=>{r.Mode="Off";RulesChanged();}),new FloatMenuOption("Best 1",()=>{r.Mode="Count";r.Amount=1;RulesChanged();}),new FloatMenuOption("Best 2",()=>{r.Mode="Count";r.Amount=2;RulesChanged();}),new FloatMenuOption("Best 3",()=>{r.Mode="Count";r.Amount=3;RulesChanged();}),new FloatMenuOption("25%",()=>{r.Mode="Percent";r.Amount=25;RulesChanged();}),new FloatMenuOption("50%",()=>{r.Mode="Percent";r.Amount=50;RulesChanged();}),new FloatMenuOption("75%",()=>{r.Mode="Percent";r.Amount=75;RulesChanged();}) }));
                    if (Widgets.ButtonText(new Rect(328, y, 105, 27), "Priority " + r.Priority)) Menu(new[]{"1","2","3","4"},v=>{r.Priority=int.Parse(v);RulesChanged();});
                    if (Widgets.ButtonText(new Rect(441, y, 130, 27), r.Backups == 0 ? "No backups" : r.Backups + " at priority " + r.BackupPriority)) Menu(new[]{"0","1","2","3"},v=>{r.Backups=int.Parse(v);RulesChanged();});
                    if (Widgets.ButtonText(new Rect(579, y, 130, 27), "Other workers " + WorkAccess.PriorityLabel(r.FallbackPriority))) Menu(new[]{"Off","3","4"},v=>{r.FallbackPriority=v=="Off"?0:int.Parse(v);RulesChanged();});
                    if (Widgets.ButtonText(new Rect(717, y, Math.Max(80, view.width - 721), 27), "Minimum skill " + r.MinSkill)) Menu(Enumerable.Range(0,21).Select(i=>i.ToString()),v=>{r.MinSkill=int.Parse(v);RulesChanged();});
                    bool adult = r.AdultsOnly, passion = r.FavorPassion, focus = r.IgnoreMainJobLimit;
                    Widgets.CheckboxLabeled(new Rect(150, y+30, 160, 24), "Adults only", ref adult);
                    Widgets.CheckboxLabeled(new Rect(328, y+30, 180, 24), "Prefer passions", ref passion);
                    Widgets.CheckboxLabeled(new Rect(579, y+30, view.width-583, 24), "Allow extra primary jobs (focus)", ref focus);
                    if (adult != r.AdultsOnly || passion != r.FavorPassion || focus != r.IgnoreMainJobLimit) { r.AdultsOnly = adult; r.FavorPassion = passion; r.IgnoreMainJobLimit = focus; RulesChanged(); }
                    y += 61;
                }
            }
            finally { Widgets.EndScrollView(); }
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
