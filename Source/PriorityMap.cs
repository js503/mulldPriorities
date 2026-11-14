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
        public bool BalancedPolicy;
        public string PresetName = "Custom";
        public bool PresetDirty;
        public int NextDailyTick;
        public int MaxMainJobs = 2;
        public float FoodTarget = 2f;
        public int LastHaulCount = -1, LastHaulTick = -1;
        private ColonySnapshot snapshot;
        private int lastScan = -9999;
        public PriorityMap(Map map) : base(map) { }
        public List<Pawn> Workers => map.mapPawns.FreeColonistsSpawned.Where(p => !p.Dead && p.workSettings != null && p.workSettings.EverWork).OrderBy(p => p.LabelShort).ToList();
        public int WorkerCount
        {
            get { int count = 0; foreach (var p in map.mapPawns.FreeColonistsSpawned) if (!p.Dead && p.workSettings != null && p.workSettings.EverWork) count++; return count; }
        }
        public bool Managed(Pawn pawn) => Excluded.Count == 0 || !Excluded.Contains(pawn.ThingID);
        public bool IsLocked(Pawn pawn, WorkTypeDef work) => Locks.Count > 0 && Locks.Contains(WorkAccess.Key(pawn, work));
        public int MainJobs(Pawn pawn) => WorkAccess.Works().Count(w => WorkAccess.Capable(pawn, w) && pawn.workSettings.GetPriority(w) is 1 or 2 && WorkAccess.MainWork(w));
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
                using var measurement = PerformanceMetrics.Measure(PerformanceOperation.NeedsScan);
                if (snapshot != null && tick - snapshot.Tick >= 2500) { LastHaulCount = snapshot.Haulables; LastHaulTick = snapshot.Tick; }
                snapshot = Needs.Read(this);
                lastScan = tick;
            }
            else PerformanceMetrics.CacheHit();
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
            using var measurement = PerformanceMetrics.Measure(PerformanceOperation.Plan);
            if (BalancedPolicy) return PlanBalanced();
            var assignments = new List<Assignment>();
            var pawns = Workers;
            var ids = pawns.ToDictionary(p => p, p => p.ThingID);
            pawns.RemoveAll(p => Excluded.Contains(ids[p]) || !WorkAccess.Available(p));
            // Main-job caps include unmanaged work and locked/temporarily boosted jobs.
            var managedDefs = Rules.Where(r => r.Enabled && r.Work != null).Select(r => r.Work).ToHashSet();
            var works = WorkAccess.Works();
            var protectedKeys = Locks.Concat(Boosts.Select(b => b.Key)).ToHashSet();
            var loads = pawns.ToDictionary(p => p, p => works.Count(w => WorkAccess.Capable(p, w) && p.workSettings.GetPriority(w) is 1 or 2 && WorkAccess.MainWork(w) && (!managedDefs.Contains(w) || protectedKeys.Contains(WorkAccess.Key(p, w)))));
            foreach (var rule in Rules.Where(r => r.Enabled && r.Work != null).OrderBy(r => r.Priority).ThenByDescending(r => r.Work.naturalPriority))
            {
                int priority = Math.Clamp(rule.Priority, 1, 4);
                var eligible = pawns.Where(p => WorkAccess.Capable(p, rule.Work)).ToList();
                var candidates = eligible.Select(p => new Candidate { Id = ids[p], Current = p.workSettings.GetPriority(rule.Work), Eligible = (!rule.AdultsOnly || WorkAccess.Adult(p)) && WorkAccess.Skill(p, rule.Work) >= rule.MinSkill, Locked = protectedKeys.Count > 0 && protectedKeys.Contains(WorkAccess.Key(p, rule.Work)), Score = WorkAccess.Score(p, rule.Work, rule.FavorPassion), MainJobs = loads[p] }).ToList();
                var planned = Planner.Assign(candidates, Planner.StaffingTarget(rule.Mode, rule.Amount, candidates.Count(c => c.Eligible)), priority, MaxMainJobs, !WorkAccess.MainWork(rule.Work) || rule.IgnoreMainJobLimit);
                foreach (var p in eligible)
                {
                    string key = WorkAccess.Key(p, rule.Work);
                    bool protect = protectedKeys.Contains(key);
                    int next = planned[ids[p]];
                    if (!protect && next is 1 or 2 && WorkAccess.MainWork(rule.Work) && !rule.IgnoreMainJobLimit) loads[p]++;
                    if (!protect && next != p.workSettings.GetPriority(rule.Work)) assignments.Add(new Assignment(p, rule.Work, p.workSettings.GetPriority(rule.Work), next, "Staffing rule: " + rule.Mode + " " + rule.Amount));
                }
            }
            return assignments;
        }
        private List<Assignment> PlanBalanced()
        {
            var workers = Workers;
            var ids = workers.ToDictionary(p => p, p => p.ThingID);
            workers.RemoveAll(p => Excluded.Contains(ids[p]) || !WorkAccess.Available(p));
            var rules = Rules.Where(r => r.Enabled && r.Work != null).ToList();
            var managed = rules.Select(r => r.Work).ToHashSet();
            var works = WorkAccess.Works();
            var protectedKeys = Locks.Concat(Boosts.Select(b => b.Key)).ToHashSet();
            var loads = workers.ToDictionary(p => p, p => works.Count(w => WorkAccess.Capable(p, w) && WorkAccess.MainWork(w) && p.workSettings.GetPriority(w) is 1 or 2 && (!managed.Contains(w) || protectedKeys.Contains(WorkAccess.Key(p, w)))));
            var inputs = rules.Select(rule =>
            {
                var candidates = workers.Where(p => WorkAccess.Capable(p, rule.Work)).Select(p => new Candidate
                {
                    Id = ids[p], Score = WorkAccess.Score(p, rule.Work, rule.FavorPassion), MainJobs = loads[p], Current = p.workSettings.GetPriority(rule.Work),
                    Eligible = (!rule.AdultsOnly || WorkAccess.Adult(p)) && WorkAccess.Skill(p, rule.Work) >= rule.MinSkill,
                    Locked = protectedKeys.Count > 0 && protectedKeys.Contains(WorkAccess.Key(p, rule.Work))
                }).ToList();
                return new ColonyWork { Id = rule.Work.defName, Candidates = candidates, Target = Planner.StaffingTarget(rule.Mode, rule.Amount, candidates.Count(c => c.Eligible)), Priority = rule.Priority,
                    Backups = rule.Mode == "Off" ? 0 : rule.Backups, BackupPriority = rule.BackupPriority, FallbackPriority = rule.Mode == "Off" ? 0 : rule.FallbackPriority,
                    Emergency = WorkAccess.Emergency(rule.Work), Essential = rule.Essential, CountsAsMain = WorkAccess.MainWork(rule.Work) && !rule.IgnoreMainJobLimit, GeneralWork = rule.Mode == "Everyone" && rule.Priority == 4 && rule.Work.defName is "Hauling" or "Cleaning", Order = -rule.Work.naturalPriority };
            }).ToList();
            var planned = Planner.AssignColony(inputs, MaxMainJobs);
            var assignments = new List<Assignment>();
            foreach (var rule in rules)
                foreach (var pawn in workers.Where(p => WorkAccess.Capable(p, rule.Work)))
                {
                    int before = pawn.workSettings.GetPriority(rule.Work), after = planned[rule.Work.defName][ids[pawn]];
                    if (after == before || protectedKeys.Count > 0 && protectedKeys.Contains(WorkAccess.Key(pawn, rule.Work))) continue;
                    string role = after == 0 ? "disabled by rule" : after <= 2 ? "primary role" : after == rule.BackupPriority ? "backup/support" : "fallback work";
                    assignments.Add(new Assignment(pawn, rule.Work, before, after, "Staffing rule: balanced " + role));
                }
            return assignments;
        }
        public string Validate(IEnumerable<Assignment> changes)
        {
            using var measurement = PerformanceMetrics.Measure(PerformanceOperation.Validate);
            var keys = new HashSet<(Pawn, WorkTypeDef)>();
            var workers = Workers.ToHashSet();
            foreach (var c in changes)
            {
                if (c == null || c.Pawn == null || c.Work == null || !c.Pawn.Spawned || c.Pawn.Map != map || !workers.Contains(c.Pawn)) return "A colonist or work type is no longer available. Refresh the plan.";
                if (!WorkAccess.Available(c.Pawn)) return c.Pawn.LabelShort + " is now unavailable. Discard and recalculate the plan.";
                if (!Managed(c.Pawn) || IsLocked(c.Pawn, c.Work) || !WorkAccess.Capable(c.Pawn, c.Work)) return c.Pawn.LabelShort + " is excluded, locked or incapable of this work.";
                if (c.After < 0 || c.After > 4 || !keys.Add((c.Pawn, c.Work))) return "The plan contains an invalid or duplicate assignment.";
                if (c.Pawn.workSettings.GetPriority(c.Work) != c.Before) return c.Pawn.LabelShort + "'s priorities changed after this draft was made. Discard and recalculate.";
                if (c.NeedKey != null && (!WorkAccess.Available(c.Pawn) || c.ExpiresAt <= Find.TickManager.TicksGame)) return "A temporary boost is stale or its colonist is unavailable. Recalculate it.";
            }
            return null;
        }
        public string Apply(List<Assignment> changes, bool enableManual)
        {
            using var measurement = PerformanceMetrics.Measure(PerformanceOperation.Apply);
            if (changes.Count == 0) return "No changes to apply.";
            string error = Validate(changes);
            if (error != null) return error;
            changes = changes.Select(c => c.Copy()).ToList();
            if (!Current.Game.playSettings.useWorkPriorities && !enableManual) return "Enable numbered work priorities before applying.";
            if (!Current.Game.playSettings.useWorkPriorities)
            {
                Current.Game.playSettings.useWorkPriorities = true;
                foreach (var m in Find.Maps) foreach (var p in m.mapPawns.AllPawns) p.workSettings?.Notify_UseWorkPrioritiesChanged();
                // Checkbox mode presents every enabled job as 3 even when its
                // stored numbered value differs. Preserve the revealed value
                // for rollback/undo, while retaining the approved proposal.
                foreach (var change in changes) change.Before = change.Pawn.workSettings.GetPriority(change.Work);
                error = Validate(changes);
                if (error != null) return "Numbered priorities enabled. " + error;
            }
            int applied = 0;
            try
            {
                foreach (var change in changes) { change.Pawn.workSettings.SetPriority(change.Work, change.After); applied++; }
            }
            catch (Exception e)
            {
                for (int i=0; i<applied; i++) changes[i].Pawn.workSettings.SetPriority(changes[i].Work, changes[i].Before);
                Log.Error("[mulld Priorities] Assignment failed and was rolled back: " + e);
                return "An assignment failed; applied changes were rolled back. See the game log.";
            }
            Undo = changes; // Apply owns the cloned batch, never the caller's preview.
            if (Boosts.Count == 0 && changes.TrueForAll(c => c.NeedKey == null))
            {
                UndoBoosts.Clear(); Invalidate(); return null;
            }
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
                if (Boosts.Count > 0 || UndoBoosts.Count > 0) RestoreUndoBoost(c);
                restored++;
            }
            Undo.Clear();
            UndoBoosts.Clear();
            Invalidate();
            return restored + " restored; " + skipped + " skipped because they changed or are now protected.";
        }
        private void RestoreUndoBoost(Assignment change)
        {
            string key = change.Key;
            Boosts.RemoveAll(b => b.Key == key);
            var previousBoost = UndoBoosts.Find(b => b.Key == key);
            if (previousBoost != null) Boosts.Add(previousBoost.Copy());
        }
        public override void MapComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (tick % 2500 != map.uniqueID % 2500) return;
            ProcessAutomation(tick);
        }
        internal void ProcessAutomation(int tick)
        {
            bool dailyDue = Daily && tick >= NextDailyTick && Current.Game.playSettings.useWorkPriorities;
            if (Boosts.Count == 0 && !dailyDue) return;
            using var measurement = PerformanceMetrics.Measure(PerformanceOperation.Automation);
            try
            {
                // Daily staffing does not need a full map/food/plant scan. Only
                // active temporary boosts need current needs for early expiry.
                var data = Boosts.Count > 0 ? Snapshot() : null;
                bool expired = false;
                foreach (var b in Boosts.ToList())
                {
                    if (b.Pawn == null || b.Work == null || b.Pawn.Dead) { Boosts.Remove(b); expired = true; continue; }
                    if (!b.Pawn.Spawned || b.Pawn.Map != map) continue;
                    bool resolved = data.Needs.Find(n => n.Key == b.NeedKey)?.Resolved ?? false;
                    if (tick < b.ExpiresAt && !resolved) continue;
                    if (WorkAccess.Capable(b.Pawn, b.Work) && Planner.CanRestore(b.Pawn.workSettings.GetPriority(b.Work), b.After, !Managed(b.Pawn) || IsLocked(b.Pawn, b.Work)))
                        b.Pawn.workSettings.SetPriority(b.Work, b.Before);
                    Boosts.Remove(b);
                    expired = true;
                }
                if (dailyDue && Compatibility.AutomaticManager(map) == null)
                {
                    NextDailyTick = tick + 60000;
                    var plan = Plan();
                    if (plan.Count > 0)
                    {
                        string error = Apply(plan, false);
                        if (error != null) Log.Warning("[mulld Priorities] Daily plan skipped: " + error);
                    }
                }
                if (expired) Invalidate();
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
            Scribe_Values.Look(ref BalancedPolicy, "mulldBalancedPolicy");
            Scribe_Values.Look(ref PresetName, "mulldPresetName", "Custom");
            Scribe_Values.Look(ref PresetDirty, "mulldPresetDirty");
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
                foreach (var rule in Rules) { rule.Priority = Math.Clamp(rule.Priority, 1, 4); rule.Amount = Math.Max(0, rule.Amount); rule.MinSkill = Math.Clamp(rule.MinSkill, 0, 20); rule.Backups = Math.Max(0, rule.Backups); rule.BackupPriority = Math.Clamp(rule.BackupPriority, 3, 4); rule.FallbackPriority = rule.FallbackPriority is 3 or 4 ? rule.FallbackPriority : 0; if (!new[] { "Count", "Percent", "Everyone", "Off" }.Contains(rule.Mode)) rule.Mode = "Count"; }
            }
        }
    }
}
