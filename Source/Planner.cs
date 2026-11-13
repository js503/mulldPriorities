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
            int remaining = Math.Max(0, target - list.Count(c => c.Locked && c.Current > 0));
            foreach (var c in list.Where(c => c.Eligible && !c.Locked && (emergency || priority > 2 || c.MainJobs < maxMainJobs))
                .OrderByDescending(c => c.Score).ThenBy(c => c.MainJobs).ThenBy(c => c.Id, StringComparer.Ordinal).Take(remaining))
                result[c.Id] = priority;
            return result;
        }

        public static bool CanRestore(int current, int applied, bool protectedNow) => !protectedNow && current == applied;
    }
}
