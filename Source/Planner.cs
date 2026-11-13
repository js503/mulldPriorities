using System;
using System.Collections.Generic;
using System.Linq;

namespace Mulld.Priorities
{
    // Pure policy: game adapters supply capability, skill and current priorities.
    public sealed class Candidate
    {
        public string Id;
        public double Score;
        public int MainJobs;
        public bool Locked;
        public int Current;
        public bool Eligible = true;
    }

    public static class Planner
    {
        public static int StaffingTarget(string mode, int amount, int eligible)
        {
            if (eligible <= 0 || mode == "Off") return 0;
            if (mode == "Everyone") return eligible;
            if (mode == "Percent") return Math.Min(eligible, (int)Math.Ceiling(eligible * Math.Clamp(amount, 0, 100) / 100d));
            return Math.Clamp(amount, 0, eligible);
        }

        public static Dictionary<string, int> Assign(IEnumerable<Candidate> candidates, int target, int priority, int maxMainJobs, bool emergency)
        {
            var list = candidates.ToList();
            var result = list.ToDictionary(c => c.Id, c => c.Locked ? c.Current : 0);
            int remaining = Math.Max(0, target - list.Count(c => c.Locked && c.Eligible && c.Current > 0 && c.Current <= priority));
            foreach (var c in list.Where(c => c.Eligible && !c.Locked && (emergency || priority > 2 || c.MainJobs < maxMainJobs))
                .OrderByDescending(c => c.Score).ThenBy(c => c.MainJobs).ThenBy(c => c.Id, StringComparer.Ordinal).Take(remaining))
                result[c.Id] = priority;
            return result;
        }

        public static bool CanRestore(int current, int applied, bool protectedNow) => !protectedNow && current == applied;

        public static Dictionary<string, Dictionary<string, int>> AssignColony(IEnumerable<ColonyWork> workRules, int maxMainJobs)
        {
            var rules = workRules.ToList();
            var loads = rules.SelectMany(r => r.Candidates).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.Max(c => c.MainJobs));
            var result = rules.ToDictionary(r => r.Id, r => r.Candidates.ToDictionary(c => c.Id, c => c.Locked ? c.Current : c.Eligible ? r.FallbackPriority : 0));
            foreach (var rule in rules.OrderByDescending(r => r.Emergency).ThenByDescending(r => r.Essential).ThenBy(r => r.Order).ThenBy(r => r.Id, StringComparer.Ordinal))
            {
                var assigned = result[rule.Id];
                int remaining = Math.Max(0, rule.Target - rule.Candidates.Count(c => c.Locked && c.Eligible && c.Current > 0 && c.Current <= rule.Priority));
                // Only the selected worker's load changes, and that worker is
                // never selected again for this role. Rank once, not once per slot.
                bool budgeted = !rule.Emergency && rule.CountsAsMain && rule.Priority <= 2;
                var overflow = budgeted ? new List<Candidate>() : null;
                var ranked = rule.Candidates.Where(c => c.Eligible && !c.Locked)
                    .OrderByDescending(c => c.Score - loads[c.Id] * 4d).ThenBy(c => loads[c.Id]).ThenBy(c => c.Id, StringComparer.Ordinal);
                foreach (var candidate in ranked)
                {
                    if (remaining <= 0) break;
                    if (budgeted && loads[candidate.Id] >= maxMainJobs) { overflow.Add(candidate); continue; }
                    assigned[candidate.Id] = rule.Priority;
                    if (budgeted) loads[candidate.Id]++;
                    remaining--;
                }
                // Coverage survives a full main-job budget at support priority.
                if (remaining > 0 && overflow != null)
                    foreach (var candidate in overflow) { if (remaining-- <= 0) break; assigned[candidate.Id] = 3; }
            }
            foreach (var rule in rules)
            {
                var assigned = result[rule.Id];
                int already = rule.Candidates.Count(c => c.Locked && c.Eligible && c.Current == rule.BackupPriority);
                foreach (var candidate in rule.Candidates.Where(c => c.Eligible && !c.Locked && (assigned[c.Id] == 0 || assigned[c.Id] > rule.BackupPriority))
                    .OrderByDescending(c => c.Score - loads[c.Id] * 2d).ThenBy(c => c.Id, StringComparer.Ordinal).Take(Math.Max(0, rule.Backups - already)))
                    assigned[candidate.Id] = rule.BackupPriority;
            }
            // Generalists still get useful main work instead of waiting behind
            // specialists' fallback assignments. Never override an explicit Off rule.
            foreach (var id in loads.Where(kv => kv.Value == 0).Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToList())
            {
                foreach (var rule in rules.Where(r => r.GeneralWork && r.Priority == 4 && r.Target > 0).OrderBy(r => r.Order).ThenBy(r => r.Id, StringComparer.Ordinal))
                {
                    var c = rule.Candidates.Find(c => c.Id == id && c.Eligible && !c.Locked);
                    if (c == null) continue;
                    result[rule.Id][id] = 2;
                    loads[id]++;
                    break;
                }
            }
            return result;
        }
    }

    public sealed class ColonyWork
    {
        public string Id;
        public int Target, Priority = 2, Backups, BackupPriority = 3, FallbackPriority, Order;
        public bool Emergency, Essential, GeneralWork, CountsAsMain = true;
        public List<Candidate> Candidates = new List<Candidate>();
    }
}
