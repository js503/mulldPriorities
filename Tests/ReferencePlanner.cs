using System;
using System.Collections.Generic;
using System.Linq;
namespace Mulld.Priorities { public static class ReferencePlanner {
        public static Dictionary<string, Dictionary<string, int>> AssignColony(IEnumerable<ColonyWork> workRules, int maxMainJobs)
        {
            var rules = workRules.ToList();
            var loads = rules.SelectMany(r => r.Candidates).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.Max(c => c.MainJobs));
            var result = rules.ToDictionary(r => r.Id, r => r.Candidates.ToDictionary(c => c.Id, c => c.Locked ? c.Current : c.Eligible ? r.FallbackPriority : 0));
            foreach (var rule in rules.OrderByDescending(r => r.Emergency).ThenByDescending(r => r.Essential).ThenBy(r => r.Order).ThenBy(r => r.Id, StringComparer.Ordinal))
            {
                var assigned = result[rule.Id];
                int remaining = Math.Max(0, rule.Target - rule.Candidates.Count(c => c.Locked && c.Eligible && c.Current > 0 && c.Current <= rule.Priority));
                var chosen = new HashSet<string>();
                while (remaining-- > 0)
                {
                    var candidates = rule.Candidates.Where(c => c.Eligible && !c.Locked && !chosen.Contains(c.Id));
                    var available = candidates.Where(c => rule.Emergency || !rule.CountsAsMain || rule.Priority > 2 || loads[c.Id] < maxMainJobs).ToList();
                    bool overflow = available.Count == 0;
                    var candidate = (overflow ? candidates : available).OrderByDescending(c => c.Score - loads[c.Id] * 4d).ThenBy(c => loads[c.Id]).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (candidate == null) break;
                    // Coverage survives a full main-job budget at support priority.
                    int priority = overflow && rule.CountsAsMain && !rule.Emergency && rule.Priority <= 2 ? 3 : rule.Priority;
                    assigned[candidate.Id] = priority;
                    if (priority <= 2 && rule.CountsAsMain && !rule.Emergency) loads[candidate.Id]++;
                    chosen.Add(candidate.Id);
                }
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
} }
