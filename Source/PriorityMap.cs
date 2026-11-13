using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Mulld.Priorities
{
    public sealed class PriorityMap : MapComponent
    {
        public List<WorkRule> Rules = new List<WorkRule>();
        public List<string> Excluded = new List<string>();
        public List<string> Locks = new List<string>();
        public List<Assignment> Boosts = new List<Assignment>();
        public List<Assignment> Undo = new List<Assignment>();
        public List<Assignment> UndoBoosts = new List<Assignment>();
        public bool Daily;
        public int NextDailyTick;
        public int MaxMainJobs = 2;
        public float FoodTarget = 2f;
        public int LastHaulCount = -1, LastHaulTick = -1;
        private ColonySnapshot snapshot;
        private int lastScan = -9999;
        public PriorityMap(Map map) : base(map) { }
        public List<Pawn> Workers => map.mapPawns.FreeColonistsSpawned.Where(p => !p.Dead && p.workSettings != null && p.workSettings.EverWork).OrderBy(p => p.LabelShort).ToList();
        public bool Managed(Pawn pawn) => !Excluded.Contains(pawn.ThingID);
        public bool IsLocked(Pawn pawn, WorkTypeDef work) => Locks.Contains(WorkAccess.Key(pawn, work));
        public int MainJobs(Pawn pawn) => WorkAccess.Works().Count(w => WorkAccess.Capable(pawn, w) && pawn.workSettings.GetPriority(w) is 1 or 2 && !WorkAccess.Emergency(w));
        public WorkRule Rule(WorkTypeDef work)
        {
            var rule = Rules.Find(r => r.Work == work);
            if (rule != null) return rule;
            rule = new WorkRule { Work = work, AdultsOnly = WorkAccess.Dangerous(work), Priority = WorkAccess.Emergency(work) ? 1 : 2, Mode = WorkAccess.Emergency(work) ? "Everyone" : "Count" };
            Rules.Add(rule);
            return rule;
        }
        public ColonySnapshot Snapshot(bool refresh = false)
        {
            int tick = Find.TickManager.TicksGame;
            if (snapshot == null || refresh || tick - lastScan >= 2500)
            {
                if (snapshot != null && tick - snapshot.Tick >= 2500) { LastHaulCount = snapshot.Haulables; LastHaulTick = snapshot.Tick; }
                snapshot = Needs.Read(this);
                lastScan = tick;
            }
            return snapshot;
        }
        public void Invalidate() { lastScan = -9999; }
        public void SetLock(Pawn pawn, WorkTypeDef work, bool enabled)
        {
            string key = WorkAccess.Key(pawn, work);
            Locks.Remove(key);
            if (enabled) Locks.Add(key);
            Invalidate();
        }
        public List<Assignment> Plan()
        {
            var assignments = new List<Assignment>();
            var pawns = Workers.Where(p => Managed(p) && WorkAccess.Available(p)).ToList();
            // Main-job caps include unmanaged work and locked/temporarily boosted jobs.
            var managedDefs = Rules.Where(r => r.Enabled && r.Work != null).Select(r => r.Work).ToHashSet();
            var loads = pawns.ToDictionary(p => p.ThingID, p => WorkAccess.Works().Count(w => WorkAccess.Capable(p, w) && p.workSettings.GetPriority(w) is 1 or 2 && !WorkAccess.Emergency(w) && (!managedDefs.Contains(w) || IsLocked(p, w) || Boosts.Any(b => b.Key == WorkAccess.Key(p, w)))));
            foreach (var rule in Rules.Where(r => r.Enabled && r.Work != null).OrderBy(r => r.Priority).ThenByDescending(r => r.Work.naturalPriority))
            {
                int priority = Math.Clamp(rule.Priority, 1, 4);
                var eligible = pawns.Where(p => WorkAccess.Capable(p, rule.Work)).ToList();
                var candidates = eligible.Select(p => new Candidate { Id = p.ThingID, Current = p.workSettings.GetPriority(rule.Work), Eligible = (!rule.AdultsOnly || WorkAccess.Adult(p)) && WorkAccess.Skill(p, rule.Work) >= rule.MinSkill, Locked = IsLocked(p, rule.Work) || Boosts.Any(b => b.Key == WorkAccess.Key(p, rule.Work)), Score = WorkAccess.Score(p, rule.Work, rule.FavorPassion), MainJobs = loads[p.ThingID] }).ToList();
                var planned = Planner.Assign(candidates, Planner.StaffingTarget(rule.Mode, rule.Amount, candidates.Count(c => c.Eligible)), priority, MaxMainJobs, WorkAccess.Emergency(rule.Work));
                foreach (var p in eligible)
                {
                    string key = WorkAccess.Key(p, rule.Work);
                    bool protect = IsLocked(p, rule.Work) || Boosts.Any(b => b.Key == key);
                    int next = planned[p.ThingID];
                    if (!protect && next is 1 or 2 && !WorkAccess.Emergency(rule.Work)) loads[p.ThingID]++;
                    if (!protect && next != p.workSettings.GetPriority(rule.Work)) assignments.Add(new Assignment(p, rule.Work, p.workSettings.GetPriority(rule.Work), next, "Staffing rule: " + rule.Mode + " " + rule.Amount));
                }
            }
            return assignments;
        }
        public string Validate(IEnumerable<Assignment> changes)
        {
            var keys = new HashSet<string>();
            foreach (var c in changes)
            {
                if (c.Pawn == null || c.Work == null || !c.Pawn.Spawned || c.Pawn.Map != map || !Workers.Contains(c.Pawn)) return "A colonist or work type is no longer available. Refresh the plan.";
                if (!Managed(c.Pawn) || IsLocked(c.Pawn, c.Work) || !WorkAccess.Capable(c.Pawn, c.Work)) return c.Pawn.LabelShort + " is excluded, locked or incapable of this work.";
                if (c.After < 0 || c.After > 4 || !keys.Add(c.Key)) return "The plan contains an invalid or duplicate assignment.";
                if (c.Pawn.workSettings.GetPriority(c.Work) != c.Before) return c.Pawn.LabelShort + "'s priorities changed after this draft was made. Discard and recalculate.";
                if (c.NeedKey != null && (!WorkAccess.Available(c.Pawn) || c.ExpiresAt <= Find.TickManager.TicksGame)) return "A temporary boost is stale or its colonist is unavailable. Recalculate it.";
            }
            return null;
        }
        public string Apply(List<Assignment> changes, bool enableManual)
        {
            if (changes.Count == 0) return "No changes to apply.";
            string error = Validate(changes);
            if (error != null) return error;
            if (!Current.Game.playSettings.useWorkPriorities && !enableManual) return "Enable numbered work priorities before applying.";
            if (!Current.Game.playSettings.useWorkPriorities)
            {
                Current.Game.playSettings.useWorkPriorities = true;
                foreach (var m in Find.Maps) foreach (var p in m.mapPawns.AllPawns) p.workSettings?.Notify_UseWorkPrioritiesChanged();
                // The notification can change how priorities are represented. Revalidate.
                error = Validate(changes);
                if (error != null) return "Numbered priorities enabled. " + error;
            }
            var applied = new List<Assignment>();
            try
            {
                foreach (var change in changes) { change.Pawn.workSettings.SetPriority(change.Work, change.After); applied.Add(change); }
            }
            catch (Exception e)
            {
                foreach (var change in applied) change.Pawn.workSettings.SetPriority(change.Work, change.Before);
                Log.Error("[mulld Priorities] Assignment failed and was rolled back: " + e);
                return "An assignment failed; applied changes were rolled back. See the game log.";
            }
            Undo = changes.Select(c => c.Copy()).ToList();
            UndoBoosts = Boosts.Where(b => changes.Any(c => c.Key == b.Key)).Select(b => b.Copy()).ToList();
            foreach (var c in changes)
            {
                var previousBoost = Boosts.Find(b => b.Key == c.Key);
                var boost = c.Copy();
                if (previousBoost != null) boost.Before = previousBoost.Before;
                Boosts.RemoveAll(b => b.Key == c.Key);
                if (c.NeedKey != null) Boosts.Add(boost);
            }
            Invalidate();
            return null;
        }
        public string UndoLast()
        {
            int restored = 0, skipped = 0;
            foreach (var c in Undo)
            {
                bool protect = c.Pawn == null || c.Work == null || !c.Pawn.Spawned || c.Pawn.Map != map || !Managed(c.Pawn) || IsLocked(c.Pawn, c.Work) || !WorkAccess.Capable(c.Pawn, c.Work);
                if (protect || !Planner.CanRestore(c.Pawn.workSettings.GetPriority(c.Work), c.After, protect)) { skipped++; continue; }
                c.Pawn.workSettings.SetPriority(c.Work, c.Before);
                Boosts.RemoveAll(b => b.Key == c.Key);
                var previousBoost = UndoBoosts.Find(b => b.Key == c.Key);
                if (previousBoost != null) Boosts.Add(previousBoost.Copy());
                restored++;
            }
            Undo.Clear();
            UndoBoosts.Clear();
            Invalidate();
            return restored + " restored; " + skipped + " skipped because they changed or are now protected.";
        }
        public override void MapComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (tick % 2500 != map.uniqueID % 2500) return;
            ProcessAutomation(tick);
        }
        internal void ProcessAutomation(int tick)
        {
            if (Boosts.Count == 0 && !Daily) return;
            try
            {
                var data = Snapshot();
                foreach (var b in Boosts.ToList())
                {
                    if (b.Pawn == null || b.Work == null || b.Pawn.Dead) { Boosts.Remove(b); continue; }
                    if (!b.Pawn.Spawned || b.Pawn.Map != map) continue;
                    bool resolved = data.Needs.Find(n => n.Key == b.NeedKey)?.Resolved ?? false;
                    if (tick < b.ExpiresAt && !resolved) continue;
                    if (WorkAccess.Capable(b.Pawn, b.Work) && Planner.CanRestore(b.Pawn.workSettings.GetPriority(b.Work), b.After, !Managed(b.Pawn) || IsLocked(b.Pawn, b.Work)))
                        b.Pawn.workSettings.SetPriority(b.Work, b.Before);
                    Boosts.Remove(b);
                }
                if (Daily && tick >= NextDailyTick)
                {
                    NextDailyTick = tick + 60000;
                    if (Current.Game.playSettings.useWorkPriorities)
                    {
                        var plan = Plan();
                        if (plan.Count > 0)
                        {
                            string error = Apply(plan, false);
                            if (error != null) Log.Warning("[mulld Priorities] Daily plan skipped: " + error);
                        }
                    }
                }
                Invalidate();
            }
            catch (Exception e) { Daily = false; Log.Error("[mulld Priorities] Automation paused after an error: " + e); }
        }
        public override void MapComponentUpdate()
        {
#if MULLD_SMOKE_TESTS
            if (GenCommandLine.CommandLineArgPassed("mulldsmoketest")) SmokeHarness.Update(this);
#endif
        }
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref Rules, "mulldRules", LookMode.Deep);
            Scribe_Collections.Look(ref Excluded, "mulldExcluded", LookMode.Value);
            Scribe_Collections.Look(ref Locks, "mulldLocks", LookMode.Value);
            Scribe_Collections.Look(ref Boosts, "mulldBoosts", LookMode.Deep);
            Scribe_Collections.Look(ref Undo, "mulldUndo", LookMode.Deep);
            Scribe_Collections.Look(ref UndoBoosts, "mulldUndoBoosts", LookMode.Deep);
            Scribe_Values.Look(ref Daily, "mulldDaily");
            Scribe_Values.Look(ref NextDailyTick, "mulldNextDailyTick");
            Scribe_Values.Look(ref MaxMainJobs, "mulldMaxMainJobs", 2);
            Scribe_Values.Look(ref FoodTarget, "mulldFoodTarget", 2f);
            Scribe_Values.Look(ref LastHaulCount, "mulldLastHaulCount", -1);
            Scribe_Values.Look(ref LastHaulTick, "mulldLastHaulTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Rules ??= new List<WorkRule>(); Rules.RemoveAll(r => r == null || r.Work == null);
                Excluded ??= new List<string>(); Locks ??= new List<string>(); Boosts ??= new List<Assignment>(); Undo ??= new List<Assignment>(); UndoBoosts ??= new List<Assignment>();
                Boosts.RemoveAll(b => b == null || b.Pawn == null || b.Work == null);
                Undo.RemoveAll(b => b == null || b.Pawn == null || b.Work == null);
                UndoBoosts.RemoveAll(b => b == null || b.Pawn == null || b.Work == null);
                MaxMainJobs = Math.Clamp(MaxMainJobs, 1, 10); FoodTarget = Math.Clamp(FoodTarget, 0.5f, 10f);
                foreach (var rule in Rules) { rule.Priority = Math.Clamp(rule.Priority, 1, 4); rule.Amount = Math.Max(0, rule.Amount); rule.MinSkill = Math.Clamp(rule.MinSkill, 0, 20); if (!new[] { "Count", "Percent", "Everyone", "Off" }.Contains(rule.Mode)) rule.Mode = "Count"; }
            }
        }
    }
}
