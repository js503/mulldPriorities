using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Mulld.Priorities
{
    public static class Needs
    {
        public static ColonySnapshot Read(PriorityMap state)
        {
            var map = state.map;
            var colonists = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Dead).ToList();
            var result = new ColonySnapshot { Tick = Find.TickManager.TicksGame };
            float prepared = 0, demand = 0;
            foreach (var pawn in colonists)
                if (pawn.needs?.food != null) demand += pawn.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed, true) * 60000f;
            foreach (var thing in map.listerThings.AllThings)
            {
                if (!thing.Spawned || thing.Position.Fogged(map) || thing.IsForbidden(Faction.OfPlayer)) continue;
                if (thing is Plant plant)
                {
                    if (plant.sown && plant.HarvestableNow && !plant.Blighted)
                    {
                        result.ReadyCrops++;
                        if (plant.Dying || plant.AmbientTemperature < 0f) result.RiskyCrops++;
                    }
                    continue;
                }
                var food = thing.def.ingestible;
                if (food == null || !thing.IngestibleNow || !FoodUtility.IsHumanFood(thing.def) || thing.def.IsDrug) continue;
                float nutrition = food.CachedNutrition * thing.stackCount;
                if (food.preferability >= FoodPreferability.MealAwful) prepared += nutrition;
                else if (FoodUtility.IsRawHumanFood(thing.def)) result.RawNutrition += nutrition;
            }
            result.FoodDays = demand > 0.001f ? prepared / demand : float.PositiveInfinity;
            var patients = map.mapPawns.FreeColonistsAndPrisonersSpawned.Concat(map.mapPawns.SlavesOfColonySpawned)
                .Distinct().Where(p => !p.Dead && p.health.HasHediffsNeedingTendByPlayer(false)).ToList();
            result.Patients = patients.Count;
            var doctor = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Doctor");
            int doctors = doctor == null ? 0 : colonists.Count(p => WorkAccess.Capable(p, doctor) && WorkAccess.Available(p) && p.workSettings.GetPriority(doctor) > 0 && !patients.Contains(p));
            var foodNeed = new ColonyNeed
            {
                Key = "food", Label = "Food runway", Value = float.IsPositiveInfinity(result.FoodDays) ? "No demand" : result.FoodDays.ToString("0.0") + " days",
                Severity = result.FoodDays < 0.5f ? 3 : result.FoodDays < state.FoodTarget ? 2 : 0,
                Status = result.FoodDays < state.FoodTarget ? "Below your " + state.FoodTarget.ToString("0.0") + " day target" : "Reserve meets target",
                Evidence = prepared.ToString("0.0") + " prepared nutrition / " + demand.ToString("0.0") + " daily demand; " + result.RawNutrition.ToString("0.0") + " raw nutrition on map.",
                WorkName = "Cooking", Resolved = result.FoodDays >= state.FoodTarget,
                Explanation = result.RawNutrition > 0 ? "Raw food is present. Giving cooking time ahead of other work may help. Check cooking bills, fuel and ingredient access too." : "Raw food is scarce. Raising cooking priority alone will not fix the shortage; check growing, hunting or trade."
            };
            if (!foodNeed.Resolved && result.RawNutrition > 0) foodNeed.Suggestion = Suggest(state, "Cooking", 2, "food", "Build the prepared food reserve", 60000);
            var careNeed = new ColonyNeed
            {
                Key = "care", Label = "Care coverage", Value = patients.Count + " : " + doctors,
                Severity = patients.Count > 0 ? (doctors == 0 ? 3 : patients.Count > doctors ? 2 : 1) : 0,
                Status = patients.Count == 0 ? "No treatment waiting" : "Patients needing tend : available assigned doctors",
                Evidence = patients.Count + " colonists/prisoners/slaves need tending. " + doctors + " assigned colonist doctors are standing, undrafted, outside mental states and not patients themselves.",
                WorkName = "Doctor", Resolved = patients.Count == 0,
                Explanation = "Coverage is a staffing indicator, not a treatment-time estimate. Sleeping doctors, access, medicine and bed availability can still delay care."
            };
            if (patients.Count > doctors) careNeed.Suggestion = Suggest(state, "Doctor", 1, "care", "Add treatment capacity", 30000, p => !patients.Contains(p));
            var harvestNeed = new ColonyNeed
            {
                Key = "harvest", Label = "Harvest pressure", Value = result.RiskyCrops > 0 ? result.RiskyCrops + " at risk" : result.ReadyCrops + " ready",
                Severity = result.RiskyCrops > 0 ? 3 : result.ReadyCrops >= 50 ? 2 : result.ReadyCrops > 0 ? 1 : 0,
                Status = result.RiskyCrops > 0 ? "Ready crops dying or below freezing" : "Harvestable sown plants",
                Evidence = result.ReadyCrops + " ready sown plants; " + result.RiskyCrops + " are dying or currently below 0 C. No weather forecast or harvest-time estimate.",
                WorkName = "Growing", Resolved = result.ReadyCrops == 0,
                Explanation = "An extra grower may help clear ready crops. Harvest permissions, allowed areas and access can still block the work. Blighted crops require cutting instead."
            };
            if (result.ReadyCrops > 0) harvestNeed.Suggestion = Suggest(state, "Growing", 2, "harvest", "Clear ready crops", 60000);
            result.Haulables = map.listerHaulables.ThingsPotentiallyNeedingHauling().Count(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer) && !t.Position.Fogged(map));
            int delta = state.LastHaulCount < 0 ? 0 : result.Haulables - state.LastHaulCount;
            var haul = new ColonyNeed
            {
                Key = "hauling", Label = "Hauling backlog", Value = result.Haulables + " stacks", WorkName = "Hauling",
                Severity = result.Haulables >= 50 && delta > 0 ? 2 : result.Haulables > 0 ? 1 : 0,
                Status = state.LastHaulCount < 0 ? "Collecting a comparison sample" : (delta >= 0 ? "+" : "") + delta + " since previous sample",
                Evidence = "Potential hauling candidates, not confirmed reachable jobs. Previous sample: " + (state.LastHaulTick < 0 ? "none" : ((result.Tick - state.LastHaulTick) / 2500f).ToString("0.0") + " in-game hours ago") + ".",
                Explanation = "If the backlog keeps growing, add hauling capacity. First check storage space, allowed areas, forbiddance and access.", Resolved = result.Haulables == 0
            };
            if (result.Haulables > 0) haul.Suggestion = Suggest(state, "Hauling", 3, "hauling", "Reduce hauling backlog", 60000);
            var essentials = new[] { "Firefighter", "Doctor", "Cooking" }.Select(DefDatabase<WorkTypeDef>.GetNamedSilentFail).Where(w => w != null).ToList();
            var uncovered = essentials.Where(w => !colonists.Any(p => WorkAccess.Capable(p, w) && WorkAccess.Available(p) && p.workSettings.GetPriority(w) > 0)).ToList();
            var coverage = new ColonyNeed { Key = "coverage", Label = "Critical coverage", Value = uncovered.Count + " gaps", Severity = uncovered.Count > 0 ? 2 : 0, Status = uncovered.Count > 0 ? string.Join(", ", uncovered.Select(w => w.labelShort)) : "Essential roles assigned", Evidence = "Firefighting, doctoring and cooking checked against standing, undrafted colonists outside mental states.", Explanation = "Assign a capable backup for uncovered essential work. Coverage does not guarantee an executable job.", Resolved = uncovered.Count == 0, WorkName = uncovered.FirstOrDefault()?.defName };
            if (uncovered.Count > 0) coverage.Suggestion = Suggest(state, uncovered[0].defName, uncovered[0].defName == "Cooking" ? 2 : 1, "coverage", "Cover essential work", 60000);
            var works = WorkAccess.Works();
            var overloaded = colonists.Where(p => p.workSettings != null && works.Count(w => WorkAccess.Capable(p, w) && p.workSettings.GetPriority(w) is 1 or 2 && !WorkAccess.Emergency(w)) > state.MaxMainJobs).ToList();
            var conflict = new ColonyNeed { Key = "commitments", Label = "Competing commitments", Value = overloaded.Count + " colonists", Severity = overloaded.Count > 0 ? 1 : 0, Status = "More than " + state.MaxMainJobs + " main jobs", Evidence = string.Join(", ", overloaded.Select(p => p.LabelShort)), Explanation = "Review these colonists' priority 1-2 jobs and move a secondary responsibility to a capable backup. This counts assignments, not measured working time.", Resolved = overloaded.Count == 0 };
            result.Needs.AddRange(new[] { foodNeed, careNeed, harvestNeed, haul, coverage, conflict });
            return result;
        }

        private static Assignment Suggest(PriorityMap state, string workName, int priority, string needKey, string reason, int duration, Func<Pawn, bool> filter = null)
        {
            var work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workName);
            if (work == null) return null;
            var candidates = state.Workers.Where(p => state.Managed(p) && WorkAccess.Capable(p, work) && WorkAccess.Available(p) && !state.IsLocked(p, work)
                && (!WorkAccess.Dangerous(work) || WorkAccess.Adult(p)) && (filter == null || filter(p))
                && (p.workSettings.GetPriority(work) == 0 || p.workSettings.GetPriority(work) > priority));
            var pawn = candidates.OrderByDescending(p => WorkAccess.Score(p, work, true) - state.MainJobs(p) * 2f).ThenBy(p => p.ThingID, StringComparer.Ordinal).FirstOrDefault();
            return pawn == null ? null : new Assignment(pawn, work, pawn.workSettings.GetPriority(work), priority, reason)
            { NeedKey = needKey, ExpiresAt = Find.TickManager.TicksGame + duration };
        }
    }
}
