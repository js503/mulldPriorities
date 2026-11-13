using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mulld.Priorities
{
    public sealed class SmokeBootstrap : Mod
    {
        public SmokeBootstrap(ModContentPack content) : base(content)
        {
            if (GenCommandLine.CommandLineArgPassed("mulldloadtest"))
                LongEventHandler.ExecuteWhenFinished(() => GameDataSaveLoader.LoadGame("mulld-compat-copy"));
        }
    }
    // Compiled only with tools/Build.ps1 -WithSmokeTests. Runs only in a new,
    // isolated -quicktest profile explicitly launched with -mulldsmoketest.
    public static class SmokeHarness
    {
        private static int phase, frames, checks;
        private static Map oldMap;
        private static string pawnId, workName;
        private static bool? originalOtherTimer;
        private static string Results => Path.Combine(GenFilePaths.SaveDataFolderPath, "smoke-results.txt");
        private static void Check(bool value, string description)
        {
            if (!value) throw new Exception(description);
            checks++; File.AppendAllText(Results, "PASS " + description + Environment.NewLine);
        }
        public static void Update(PriorityMap state)
        {
            if (state.map != Find.CurrentMap || phase == 99) return;
            try
            {
                if (phase == 0)
                {
                    if (state.Workers.Count < 1 || Find.TickManager == null) return;
                    phase = 1; oldMap = state.map;
                    File.WriteAllText(Results, "mulld Priorities in-game smoke test" + Environment.NewLine);
                    File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "loaded-mods.txt"), string.Join(Environment.NewLine, LoadedModManager.RunningMods.Select(m => m.PackageId + " | " + m.Name)));
                    Check(!state.Daily && state.Boosts.Count == 0, "Automation starts disabled");
                    var snapshot = state.Snapshot(true);
                    var otherComponent = state.map.components.FirstOrDefault(c => c.GetType().FullName == "AutoPriorities.Core.MapSpecificData");
                    var otherTimer = otherComponent?.GetType().GetProperty("RunOnTimer");
                    originalOtherTimer = otherTimer == null ? null : (bool?)otherTimer.GetValue(otherComponent);
                    File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "compat-details.txt"), "Original AutoPriorities timer: " + originalOtherTimer + Environment.NewLine + "Visible work types: " + string.Join(", ", WorkAccess.Works().Select(w => w.defName)) + Environment.NewLine + "Workers: " + state.Workers.Count + Environment.NewLine);
                    var storage = state.map.listerThings.AllThings.Where(t => t.GetType().Namespace == "AdaptiveStorage").ToList();
                    File.AppendAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "compat-details.txt"), "AdaptiveStorage buildings: " + storage.Count + Environment.NewLine);
                    if (storage.Count > 0) File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "storage-members.txt"), string.Join(Environment.NewLine, storage[0].GetType().GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Select(m => m.ToString())));
                    foreach (var building in storage)
                    {
                        var contents = building.GetType().GetProperty("StoredThings")?.GetValue(building) as System.Collections.IEnumerable;
                        if (contents == null) continue;
                        var storedFood = contents.Cast<Thing>().Where(t => t.def.ingestible != null && FoodUtility.IsHumanFood(t.def)).ToList();
                        File.AppendAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "compat-details.txt"), building.def.defName + " food stacks: " + storedFood.Count + "; unspawned: " + storedFood.Count(t => !t.Spawned) + Environment.NewLine);
                        Check(storedFood.All(t => state.map.listerThings.AllThings.Contains(t)), "Adaptive Storage food remains visible to the indicator scan");
                    }
                    var fridgeDef = DefDatabase<ThingDef>.GetNamedSilentFail("sbz_Fridge");
                    if (fridgeDef != null)
                    {
                        var fridgeCell = state.map.AllCells.First(c => GenAdj.OccupiedRect(c, Rot4.North, fridgeDef.size).Cells.All(cell => cell.InBounds(state.map) && cell.Standable(state.map) && !cell.Fogged(state.map) && cell.GetEdifice(state.map) == null));
                        var fridge = (Building_Storage)GenSpawn.Spawn(ThingMaker.MakeThing(fridgeDef, fridgeDef.MadeFromStuff ? ThingDefOf.Steel : null), fridgeCell, state.map);
                        fridge.SetFaction(Faction.OfPlayer);
                        fridge.GetStoreSettings().filter.SetAllow(ThingDefOf.MealSimple, true);
                        var foodBefore = state.Snapshot(true).FoodDays;
                        var fridgeMeal = ThingMaker.MakeThing(ThingDefOf.MealSimple); fridgeMeal.stackCount = 3;
                        GenSpawn.Spawn(fridgeMeal, fridge.GetSlotGroup().CellsList.First(), state.map);
                        var contents = fridge.GetType().GetProperty("StoredThings")?.GetValue(fridge) as System.Collections.IEnumerable;
                        Check(contents != null && contents.Cast<Thing>().Contains(fridgeMeal), "Test meals registered inside sbz Adaptive Storage fridge");
                        float dailyFood = state.map.mapPawns.FreeColonistsSpawned.Where(p => p.needs?.food != null).Sum(p => p.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed, true) * 60000f);
                        float measuredNutrition = (state.Snapshot(true).FoodDays - foodBefore) * dailyFood;
                        Check(Math.Abs(measuredNutrition - fridgeMeal.def.ingestible.CachedNutrition * fridgeMeal.stackCount) < .02f, "Fridge meals counted exactly once in food runway");
                    }
                    Check(snapshot.Needs.Count == 6, "All six live colony indicators computed");
                    Check(snapshot.FoodDays >= 0 && snapshot.ReadyCrops >= 0, "Indicator values nonnegative");
                    var pawn = state.Workers.First();
                    var work = WorkAccess.Works().First(w => WorkAccess.Capable(pawn, w));
                    Check(WorkAccess.Works().All(w => !string.IsNullOrWhiteSpace(WorkAccess.Label(w))), "Every work row has a visible label");
                    Check(WorkAccess.Emergency(DefDatabase<WorkTypeDef>.GetNamed("PatientBedRest")), "Bed rest exempt from main-job cap");
                    pawnId = pawn.ThingID; workName = work.defName;
                    Current.Game.playSettings.useWorkPriorities = true;
                    foreach (var p in state.Workers) p.workSettings.Notify_UseWorkPrioritiesChanged();
                    int before = pawn.workSettings.GetPriority(work), after = before == 3 ? 4 : 3;
                    var edit = new Assignment(pawn, work, before, after, "Smoke test");
                    Check(state.Validate(new[] { edit }) == null, "Valid draft accepted");
                    Check(state.Apply(new List<Assignment> { edit }, false) == null && pawn.workSettings.GetPriority(work) == after, "Manual draft applies to real pawn");
                    state.UndoLast();
                    Check(pawn.workSettings.GetPriority(work) == before, "Undo restores real priority");
                    state.SetLock(pawn, work, true);
                    Check(state.Validate(new[] { edit }) != null, "Locked assignment rejected");
                    state.SetLock(pawn, work, false);
                    state.Excluded.Add(pawnId);
                    Check(state.Validate(new[] { edit }) != null, "Excluded colonist rejected");
                    state.Excluded.Clear();
                    var bad = edit.Copy(); bad.Before = before == 0 ? 1 : 0;
                    Check(state.Validate(new[] { bad }) != null, "Stale draft rejected");
                    bad = edit.Copy(); bad.After = 5;
                    Check(state.Validate(new[] { bad }) != null, "Out-of-range priority rejected");
                    var boost = edit.Copy(); boost.NeedKey = "smoke"; boost.ExpiresAt = Find.TickManager.TicksGame + 1;
                    Check(state.Apply(new List<Assignment> { boost }, false) == null && state.Boosts.Count == 1, "Temporary boost tracked");
                    state.ProcessAutomation(Find.TickManager.TicksGame + 2500);
                    Check(pawn.workSettings.GetPriority(work) == before && state.Boosts.Count == 0, "Expired boost restores original priority");
                    boost.ExpiresAt = Find.TickManager.TicksGame + 1;
                    state.Apply(new List<Assignment> { boost }, false);
                    pawn.workSettings.SetPriority(work, before);
                    state.ProcessAutomation(Find.TickManager.TicksGame + 2500);
                    Check(pawn.workSettings.GetPriority(work) == before && state.Boosts.Count == 0, "External edit survives boost expiry");
                    var rule = state.Rule(work); rule.Enabled = true; rule.Mode = "Everyone"; rule.Priority = 3;
                    var plan = state.Plan();
                    Check(state.Validate(plan) == null, "Staffing plan validates against live colony");
                    state.SetLock(pawn, work, true);
                    Check(state.Plan().All(c => c.Pawn != pawn || c.Work != work), "Rule planner preserves real lock");
                    state.SetLock(pawn, work, false);
                    state.Excluded.Add(pawnId);
                    Check(state.Plan().All(c => c.Pawn != pawn), "Rule planner preserves real exclusion");
                    state.Excluded.Clear();
                    int now = Find.TickManager.TicksGame;
                    boost.ExpiresAt = now + 1;
                    Check(state.Apply(new List<Assignment> { boost }, false) == null, "Boost applies before lock-expiry test");
                    state.SetLock(pawn, work, true);
                    state.ProcessAutomation(now + 2500);
                    Check(pawn.workSettings.GetPriority(work) == after && state.Boosts.Count == 0, "New lock prevents expiry overwrite");
                    state.SetLock(pawn, work, false); pawn.workSettings.SetPriority(work, before);
                    boost.ExpiresAt = now + 60000;
                    state.Apply(new List<Assignment> { boost }, false);
                    int secondPriority = after == 2 ? 4 : 2;
                    var secondBoost = new Assignment(pawn, work, after, secondPriority, "Second temporary boost") { NeedKey = "smoke", ExpiresAt = now + 1 };
                    Check(state.Apply(new List<Assignment> { secondBoost }, false) == null && state.Boosts.Single().Before == before, "Replacing boost retains original restore priority");
                    state.UndoLast();
                    Check(pawn.workSettings.GetPriority(work) == after && state.Boosts.Single().After == after, "Undo reinstates previous boost record");
                    state.ProcessAutomation(now + 70000);
                    Check(pawn.workSettings.GetPriority(work) == before && state.Boosts.Count == 0, "Reinstated boost restores original priority");
                    boost.ExpiresAt = Find.TickManager.TicksGame + 60000;
                    Check(state.Apply(new List<Assignment> { boost }, false) == null, "Persistent boost staged for save");
                    state.SetLock(pawn, work, true);
                    state.FoodTarget = 3.5f; state.MaxMainJobs = 3;
                    if (state.Workers.Count > 1) state.Excluded.Add(state.Workers[1].ThingID);
                    state.Daily = true; state.NextDailyTick = Find.TickManager.TicksGame + 60000;
                    Check(Presets.Export(state).StartsWith("Exported"), "Rules export");
                    state.Rules.Clear();
                    Check(Presets.Import(state).StartsWith("Imported") && state.Rule(work).Enabled, "Rules import");
                    var meal = ThingMaker.MakeThing(ThingDefOf.MealSimple); meal.stackCount = 6;
                    GenSpawn.Spawn(meal, pawn.Position, state.map);
                    float originalDays = snapshot.FoodDays;
                    var withMeals = state.Snapshot(true);
                    Check(withMeals.FoodDays > originalDays, "Prepared food increases measured runway");
                    meal.SetForbidden(true);
                    Check(state.Snapshot(true).FoodDays < withMeals.FoodDays, "Forbidden meals excluded from runway");
                    meal.SetForbidden(false);
                    var raw = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RawPotatoes")); raw.stackCount = Math.Min(50, raw.def.stackLimit);
                    GenSpawn.Spawn(raw, pawn.Position, state.map);
                    Check(state.Snapshot(true).RawNutrition > 0, "Raw ingredients measured separately");
                    var plantCell = state.map.AllCells.First(c => c.Standable(state.map) && !c.Fogged(state.map) && c.GetPlant(state.map) == null && c.GetTerrain(state.map).fertility >= .6f);
                    var crop = (Plant)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("Plant_Rice"), plantCell, state.map);
                    crop.sown = true; crop.Growth = 1f;
                    Check(state.Snapshot(true).ReadyCrops >= 1, "Harvestable sown crop detected");
                    var wound = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, pawn.RaceProps.body.corePart); wound.Severity = 2f; pawn.health.AddHediff(wound);
                    Check(state.Snapshot(true).Patients >= 1, "Untreated real wound counted in care coverage");
                    GameDataSaveLoader.SaveGame("mulld-smoke");
                    Check(File.Exists(GenFilePaths.FilePathForSavedGame("mulld-smoke")), "Game save written");
                    phase = 2;
                    LongEventHandler.QueueLongEvent(() => GameDataSaveLoader.LoadGame("mulld-smoke"), "LoadingLongEvent", false, e => Fail(e));
                }
                else if (phase == 2 && state.map != oldMap)
                {
                    phase = 3;
                    var pawn = state.Workers.First(p => p.ThingID == pawnId);
                    var work = DefDatabase<WorkTypeDef>.GetNamed(workName);
                    Check(state.Daily && state.FoodTarget == 3.5f && state.MaxMainJobs == 3, "Settings survive save and reload");
                    Check(state.Rule(work).Enabled && state.Rule(work).Mode == "Everyone", "Staffing rule survives save and reload");
                    Check(state.IsLocked(pawn, work), "Lock survives save and reload");
                    Check(state.Workers.Count < 2 || !state.Managed(state.Workers[1]), "Exclusion survives save and reload");
                    Check(state.Boosts.Count == 1 && state.Undo.Count == 1, "Boost and undo history survive reload");
                    Check(state.Snapshot(true).Needs.Count == 6, "Indicators compute after reload");
                    state.Boosts.Clear(); // Expiry has its own checks; isolate the daily-manager conflict test.
                    state.SetLock(pawn, work, false); // Persistence is checked above; exercise an editable worker even in a solo colony.
                    int tick = Find.TickManager.TicksGame;
                    var otherComponent = state.map.components.FirstOrDefault(c => c.GetType().FullName == "AutoPriorities.Core.MapSpecificData");
                    var otherTimer = otherComponent?.GetType().GetProperty("RunOnTimer");
                    if (otherTimer != null)
                    {
                        otherTimer.SetValue(otherComponent, true);
                        var priorities = state.Workers.Where(p => WorkAccess.Capable(p, work)).ToDictionary(p => p.ThingID, p => p.workSettings.GetPriority(work));
                        int scheduled = state.NextDailyTick;
                        state.ProcessAutomation(tick + 70000);
                        Check(Compatibility.AutomaticManager(state.map) != null && state.NextDailyTick == scheduled, "AutoPriorities timer pauses mulld daily rules");
                        Check(state.Workers.Where(p => WorkAccess.Capable(p, work)).All(p => p.workSettings.GetPriority(work) == priorities[p.ThingID]), "Manager-overlap guard preserves every work assignment");
                        Check((bool)otherTimer.GetValue(otherComponent), "Overlap guard does not disable the other mod's timer");
                        otherTimer.SetValue(otherComponent, false);
                    }
                    state.ProcessAutomation(tick + 70000);
                    Check(state.NextDailyTick == tick + 130000, "Daily reassignment advances next-run timer");
                    var dailyWorkers = state.Workers.Where(p => state.Managed(p) && WorkAccess.Capable(p, work) && !state.IsLocked(p, work) && WorkAccess.Available(p)).ToList();
                    Check(dailyWorkers.Count > 0 && dailyWorkers.All(p => p.workSettings.GetPriority(work) == 3), "Daily staffing rules apply to eligible real pawns");
                    state.Daily = false;
                    if (otherTimer != null && originalOtherTimer.HasValue) otherTimer.SetValue(otherComponent, originalOtherTimer.Value);
                    Find.MainTabsRoot.SetCurrentTab(DefDatabase<MainButtonDef>.GetNamed("MulldPriorities"));
                    Find.TickManager.Pause();
                    frames = 0;
                }
                else if (phase == 3)
                {
                    frames++;
                    var window = (MainTabWindow_Priorities)DefDatabase<MainButtonDef>.GetNamed("MulldPriorities").TabWindow;
                    var work = DefDatabase<WorkTypeDef>.GetNamed(workName);
                    if (frames == 30) ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "mulld-main-interface.png"));
                    if (frames == 45) window.SmokeView("Work", work);
                    if (frames == 75) ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "mulld-editor.png"));
                    if (frames == 90) window.SmokeView("Colonists");
                    if (frames == 120) ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "mulld-colonists.png"));
                    if (frames == 135) window.SmokeView("Settings");
                    if (frames == 165) ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "mulld-settings.png"));
                    if (frames == 180) window.SmokeView("Work", work, true);
                    if (frames == 210) ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "mulld-review.png"));
                    if (frames == 240)
                    {
                        var errors = Log.Messages.Where(m => m.type == LogMessageType.Error).ToList();
                        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "game-messages.txt"), string.Join(Environment.NewLine, Log.Messages.Select(m => m.type + ": " + m.text + Environment.NewLine + m.StackTrace)));
                        Check(errors.Count == 0, "No game errors during load, reload and five interface views");
                        File.AppendAllText(Results, "COMPLETE " + checks + " in-game checks passed." + Environment.NewLine);
                        phase = 99; Application.Quit();
                    }
                }
            }
            catch (Exception e) { Fail(e); }
        }
        private static void Fail(Exception e)
        {
            phase = 99;
            File.AppendAllText(Results, "FAIL " + e + Environment.NewLine);
            Log.Error("[mulld Priorities smoke] " + e);
            Application.Quit(1);
        }
    }
}
