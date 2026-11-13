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
        private readonly Dictionary<string, Assignment> draft = new Dictionary<string, Assignment>();
        private Map boundMap;
        private string selected = "food", page = "Needs", status = "Select a need, or preview a balanced plan.";
        private Vector2 scroll;
        private bool drawingNative, resizePending, review, enableNumbered;
        public bool Expanded = true;
        internal static MainTabWindow_Priorities Drawing;
        internal bool Reviewing => Expanded && review;
        private PriorityMap State => Find.CurrentMap?.GetComponent<PriorityMap>();
        private static readonly Color Gold = new Color(.86f, .71f, .44f);
        private static readonly Color Muted = new Color(.72f, .76f, .72f);
        private float PanelHeight => !Expanded ? 0 : Math.Min(review ? 192 : page == "Needs" ? 230 : 330, Math.Max(192, UI.screenHeight * .46f));
        protected override float ExtraTopSpace => base.ExtraTopSpace + (drawingNative ? 0 : PanelHeight);
        public override void PostOpen() { Bind(); base.PostOpen(); }
        private void Bind()
        {
            if (boundMap == Find.CurrentMap) return;
            boundMap = Find.CurrentMap; draft.Clear(); review = false; scroll = Vector2.zero;
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
                    if (Widgets.ButtonText(new Rect(rect.width - 184, 4, 180, 28), "mulld Priorities " + (Expanded ? "▴" : "▾")))
                    { Expanded = !Expanded; resizePending = true; }
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
            Text.Font = GameFont.Small; GUI.color = Color.white; Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Medium; Label(new Rect(5, 0, 240, 30), review ? "Review in grid" : "mulld Priorities"); Text.Font = GameFont.Small;
            string manager = Compatibility.AutomaticManager(boundMap);
            string context = review ? draft.Values.Select(c => c.Pawn).Distinct().Count() + " pawns · " + draft.Count + " changes · Not applied" : State.Workers.Count + (State.Workers.Count == 1 ? " worker" : " workers") + " · " + (manager != null ? manager + " timer active; mulld daily rules paused" : State.Daily ? "Daily balanced/rule assignment" : "Manual approval");
            Label(new Rect(5, 30, area.width - 10, 23), context, Muted);
            if (!review)
            {
                float x = area.width - 376;
                foreach (var tab in new[] { "Needs", "Rules", "Colonists", "Settings" })
                {
                    if (Widgets.ButtonText(new Rect(x, 1, 90, 27), page == tab ? "[" + tab + "]" : tab)) { page = tab; scroll = Vector2.zero; resizePending = true; }
                    x += 94;
                }
            }
            Rect body = new Rect(5, 58, area.width - 10, Math.Max(30, area.height - 128));
            if (review) DrawReview(body);
            else if (page == "Needs") DrawNeeds(body);
            else if (page == "Rules") DrawRules(body);
            else if (page == "Colonists") DrawColonists(body);
            else DrawSettings(body);
            DrawFooter(new Rect(5, area.height - 63, area.width - 10, 57));
            Widgets.DrawBoxSolid(new Rect(0, area.height - 2, area.width, 2), Gold);
        }
        private void DrawNeeds(Rect area)
        {
            var snapshot = State.Snapshot();
            var choices = snapshot.Needs.Where(n => n.Key is "food" or "care" or "harvest").ToList();
            var active = snapshot.Needs.Find(n => n.Key == selected) ?? choices[0];
            if (!choices.Contains(active)) choices[2] = active;
            float cardHeight = Math.Min(59, area.height - 30);
            for (int i = 0; i < choices.Count; i++)
            {
                var need = choices[i]; var box = new Rect(area.x + i * (area.width + 9) / 3, area.y, (area.width - 18) / 3, cardHeight);
                Widgets.DrawMenuSection(box);
                if (need.Key == selected) Widgets.DrawBoxSolid(new Rect(box.x, box.y, 3, box.height), Gold);
                Label(new Rect(box.x + 10, box.y + 2, box.width - 20, 22), need.Label, need.Severity > 1 ? Gold : Muted);
                Text.Font = GameFont.Medium; Label(new Rect(box.x + 10, box.y + 24, box.width - 20, 30), need.Value); Text.Font = GameFont.Small;
                TooltipHandler.TipRegion(box, need.Status + "\n" + need.Evidence);
                if (Widgets.ButtonInvisible(box)) selected = need.Key;
            }
            Label(new Rect(area.x + 4, area.y + cardHeight + 6, area.width - 210, 24), active.Resolved ? active.Status : active.Explanation, Muted);
            var suggestion = active.Suggestion;
            if (Widgets.ButtonText(new Rect(area.xMax - 195, area.y + cardHeight + 3, 190, 27), active.Resolved ? "Need covered" : suggestion == null ? "No safe boost" : "Preview " + suggestion.Pawn.LabelShort + " boost", active: suggestion != null))
            { Stage(suggestion.Copy()); BeginReview(); }
        }
        private void DrawReview(Rect area)
        {
            Label(new Rect(area.x, area.y - 1, area.width, 24), "Gold cells show current → proposed.  – = Off.  Other assignments stay unchanged.", Gold);
            if (!Current.Game.playSettings.useWorkPriorities)
                Widgets.CheckboxLabeled(new Rect(area.x, area.y + 24, area.width - 5, 27), "Enable numbered work priorities for this save when applying", ref enableNumbered);
            else
            {
                var gaps = new[] { "Doctor", "Cooking", "Growing", "Construction" }.Select(DefDatabase<WorkTypeDef>.GetNamedSilentFail).Where(w => w != null)
                    .Where(w => !State.Workers.Any(p => WorkAccess.Capable(p, w) && WorkAccess.Available(p) && PreviewPriority(p, w) > 0)).ToList();
                Label(new Rect(area.x, area.y + 24, area.width, 24), gaps.Count == 0 ? "Essential roles stay covered in this draft." : "No available assigned worker: " + string.Join(", ", gaps.Select(WorkAccess.Label)), gaps.Count == 0 ? Muted : Gold);
            }
        }
        private void DrawFooter(Rect area)
        {
            Label(new Rect(area.x, area.y, area.width - 4, 23), status, Muted);
            float y = area.y + 26;
            if (!review)
            {
                if (Widgets.ButtonText(new Rect(area.x, y, 142, 28), "Balanced plan")) { Presets.Balanced(State); StageRules(); BeginReview(); }
                if (Widgets.ButtonText(new Rect(area.x + 150, y, 140, 28), "Preview rules")) { StageRules(); BeginReview(); }
            }
            else if (Widgets.ButtonText(new Rect(area.x, y, 130, 28), "Back to editing")) { review = false; resizePending = true; }
            float x = area.xMax - 448;
            if (Widgets.ButtonText(new Rect(x, y, 92, 28), "Discard", active: draft.Count > 0)) { draft.Clear(); review = false; resizePending = true; status = "Draft discarded; actual priorities unchanged."; }
            if (Widgets.ButtonText(new Rect(x + 100, y, 112, 28), "Undo apply", active: State.Undo.Count > 0 && draft.Count == 0)) { status = State.UndoLast(); }
            string label = review ? "Apply " + draft.Count + " changes" : "Review " + draft.Count + " changes";
            if (Widgets.ButtonText(new Rect(x + 220, y, 228, 28), label, active: draft.Count > 0 && (!review || Current.Game.playSettings.useWorkPriorities || enableNumbered)))
            {
                if (!review) BeginReview();
                else
                {
                    string error = State.Apply(draft.Values.Select(c => c.Copy()).ToList(), enableNumbered);
                    if (error != null) status = error;
                    else { draft.Clear(); review = false; resizePending = true; status = "Priorities applied. Previous assignments saved for undo."; }
                }
            }
        }
        private void BeginReview()
        {
            review = draft.Count > 0; enableNumbered = false; resizePending = true;
            status = review ? "Review only: the game still uses your current priorities until you apply." : "No assignment changes are needed for these rules.";
        }
        private void StageRules()
        {
            foreach (var key in draft.Where(kv => kv.Value.Reason.StartsWith("Staffing rule:", StringComparison.Ordinal)).Select(kv => kv.Key).ToList()) draft.Remove(key);
            foreach (var change in State.Plan()) if (!draft.ContainsKey(change.Key)) Stage(change);
        }
        private void Stage(Assignment change)
        {
            if (draft.TryGetValue(change.Key, out var previous)) change.Before = previous.Before;
            if (change.Before == change.After) draft.Remove(change.Key); else draft[change.Key] = change;
        }
        private int PreviewPriority(Pawn pawn, WorkTypeDef work) => draft.TryGetValue(WorkAccess.Key(pawn, work), out var c) ? c.After : pawn.workSettings.GetPriority(work);
        internal bool TryPreview(Pawn pawn, WorkTypeDef work, out Assignment change) => draft.TryGetValue(WorkAccess.Key(pawn, work), out change);
        private static void Menu(IEnumerable<string> items, Action<string> setter) => Find.WindowStack.Add(new FloatMenu(items.Select(item => new FloatMenuOption(item, () => setter(item))).ToList()));
        private void DrawRules(Rect area)
        {
            var works = WorkAccess.Works().OrderByDescending(w => w.naturalPriority).ToList();
            var view = new Rect(0, 0, area.width - 20, works.Count * 61 + 28);
            Widgets.BeginScrollView(area, ref scroll, view);
            try
            {
                Label(new Rect(2, 0, view.width, 23), "Primary roles · Support backups · Fallback when main work is done", Muted);
                float y = 29;
                foreach (var work in works)
                {
                    var r = State.Rule(work);
                    Widgets.CheckboxLabeled(new Rect(2, y, 142, 27), WorkAccess.Label(work), ref r.Enabled);
                    if (Widgets.ButtonText(new Rect(150, y, 170, 27), r.Mode + (r.Mode is "Count" or "Percent" ? " " + r.Amount : "")))
                        Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption> { new FloatMenuOption("Everyone",()=>r.Mode="Everyone"),new FloatMenuOption("Off",()=>r.Mode="Off"),new FloatMenuOption("Best 1",()=>{r.Mode="Count";r.Amount=1;}),new FloatMenuOption("Best 2",()=>{r.Mode="Count";r.Amount=2;}),new FloatMenuOption("Best 3",()=>{r.Mode="Count";r.Amount=3;}),new FloatMenuOption("25%",()=>{r.Mode="Percent";r.Amount=25;}),new FloatMenuOption("50%",()=>{r.Mode="Percent";r.Amount=50;}),new FloatMenuOption("75%",()=>{r.Mode="Percent";r.Amount=75;}) }));
                    if (Widgets.ButtonText(new Rect(328, y, 105, 27), "Primary " + r.Priority)) Menu(new[]{"1","2","3","4"},v=>r.Priority=int.Parse(v));
                    if (Widgets.ButtonText(new Rect(441, y, 130, 27), r.Backups + " backups at " + r.BackupPriority)) Menu(new[]{"0","1","2","3"},v=>r.Backups=int.Parse(v));
                    if (Widgets.ButtonText(new Rect(579, y, 130, 27), "Fallback " + WorkAccess.PriorityLabel(r.FallbackPriority))) Menu(new[]{"Off","3","4"},v=>r.FallbackPriority=v=="Off"?0:int.Parse(v));
                    if (Widgets.ButtonText(new Rect(717, y, Math.Max(80, view.width - 721), 27), "Skill ≥ " + r.MinSkill)) Menu(Enumerable.Range(0,21).Select(i=>i.ToString()),v=>r.MinSkill=int.Parse(v));
                    bool adult = r.AdultsOnly, passion = r.FavorPassion;
                    Widgets.CheckboxLabeled(new Rect(150, y+30, 160, 24), "Adults only", ref adult); r.AdultsOnly = adult;
                    Widgets.CheckboxLabeled(new Rect(328, y+30, 180, 24), "Favor passions", ref passion); r.FavorPassion = passion;
                    Label(new Rect(579, y+30, view.width-583, 24), r.Enabled ? r.Essential ? "Essential coverage retained at support priority if main slots are full" : "Rule saved; preview before applying" : "Manual assignments preserved", Muted);
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
                    bool managed = State.Managed(pawn), before = managed;
                    Widgets.CheckboxLabeled(new Rect(5,y,view.width*.45f,28),pawn.LabelShort + (WorkAccess.Available(pawn)?"":" · unavailable"),ref managed);
                    if (managed != before) { State.Excluded.Remove(pawn.ThingID); if(!managed)State.Excluded.Add(pawn.ThingID); foreach(var key in draft.Where(kv=>kv.Value.Pawn==pawn).Select(kv=>kv.Key).ToList())draft.Remove(key); State.Invalidate(); }
                    Label(new Rect(view.width*.48f,y+2,view.width*.25f,25),State.MainJobs(pawn)+" main assignments",Muted);
                    if(Widgets.ButtonText(new Rect(view.width*.76f,y,view.width*.23f,27),"Assignment locks"))
                        Find.WindowStack.Add(new FloatMenu(WorkAccess.Works().Where(w=>WorkAccess.Capable(pawn,w)).Select(w=>new FloatMenuOption((State.IsLocked(pawn,w)?"Unlock ":"Lock ")+WorkAccess.Label(w),()=>{State.SetLock(pawn,w,!State.IsLocked(pawn,w));draft.Remove(WorkAccess.Key(pawn,w));})).ToList()));
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
                list.CheckboxLabeled("Use balanced primary / backup / fallback policy",ref State.BalancedPolicy);
                list.Label("Main-role budget: "+State.MaxMainJobs+" per pawn (essential overflow stays enabled at priority 3)");
                int mainJobs=(int)Math.Round(list.Slider(State.MaxMainJobs,1,10));
                if(mainJobs!=State.MaxMainJobs){State.MaxMainJobs=mainJobs;State.Invalidate();}
                list.Label("Food reserve target: "+State.FoodTarget.ToString("0.0")+" days");
                float target=(float)Math.Round(list.Slider(State.FoodTarget,.5f,10f)*2)/2;
                if(Math.Abs(target-State.FoodTarget)>.01){State.FoodTarget=target;State.Invalidate();}
                if(list.ButtonText("All colony needs"))Find.WindowStack.Add(new FloatMenu(State.Snapshot().Needs.Select(n=>new FloatMenuOption(n.Label+": "+n.Value,()=>{selected=n.Key;page="Needs";resizePending=true;})).ToList()));
                if(list.ButtonText("Export staffing rules"))status=Presets.Export(State);
                if(list.ButtonText("Import staffing rules"))status=Presets.Import(State);
                list.Label("Locks, exclusions and active boosts are protected. Unknown mod work, hunting and dark study remain manual in the balanced preset. Indicators are estimates; bills, storage and access may block jobs.");
                list.End();
            }
            finally { Widgets.EndScrollView(); }
        }
#if MULLD_SMOKE_TESTS
        internal void SmokeView(string tab, WorkTypeDef work = null, bool preview = false)
        {
            Expanded=true;page=tab=="Work"?"Needs":tab;scroll=Vector2.zero;review=false;
            if(preview)
            {
                Presets.Balanced(State);StageRules();
                if(draft.Count==0){var p=State.Workers.First(p=>State.Managed(p)&&WorkAccess.Capable(p,work)&&!State.IsLocked(p,work));int before=p.workSettings.GetPriority(work);Stage(new Assignment(p,work,before,before==3?4:3,"Smoke review"));}
                BeginReview();
            }
            resizePending=true;
        }
        internal int SmokeDraftCount => draft.Count;
        internal void SmokeCollapse() { Expanded=false;resizePending=true; }
        internal string SmokeApply()
        {
            string error=State.Apply(draft.Values.Select(c=>c.Copy()).ToList(),true);
            if(error==null){draft.Clear();review=false;resizePending=true;status="Balanced assignments applied; undo available.";}
            return error;
        }
#endif
    }
}
