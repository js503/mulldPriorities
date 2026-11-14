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
            if (GenCommandLine.CommandLineArgPassed("mulldbenchmark")) { PerformanceBenchmark.Update(state); return; }
            if (state.map != Find.CurrentMap || phase == 99) return;
            try
            {
                if (phase == 0)
                {
                    if (state.Workers.Count < 1 || Find.TickManager == null) return;
                    phase = 1; oldMap = state.map;
                    File.WriteAllText(Results, "mulld Priorities in-game smoke test" + Environment.NewLine);
                    string legacySettings=Path.Combine(GenFilePaths.ConfigFolderPath,"Mod_muldPriorities_MulldMod.xml");
                    if(File.Exists(legacySettings))
                    {
                        bool expected=(bool?)System.Xml.Linq.XElement.Load(legacySettings).Descendants("performanceDiagnostics").FirstOrDefault()??false;
                        Check(File.Exists(Path.Combine(GenFilePaths.ConfigFolderPath,"Mod_mulldPriorities_MulldMod.xml"))&&MulldMod.Settings.PerformanceDiagnostics==expected,"Renamed mod folder preserves legacy Mod Settings on startup");
                    }
                    File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "loaded-mods.txt"), string.Join(Environment.NewLine, LoadedModManager.RunningMods.Select(m => m.PackageId + " | " + m.Name)));
                    if (!GenCommandLine.CommandLineArgPassed("mulldloadtest")) Check(!state.Daily && state.Boosts.Count == 0, "Automation starts disabled");
                    else Check(state != null, "Existing colony map gains the integrated Work panel");
                    state.Daily=false;state.Boosts.Clear();state.Undo.Clear();state.UndoBoosts.Clear();state.Rules=new List<WorkRule>();state.BalancedPolicy=false;
                    AuditDiagnostics(state);
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
                    AuditNamedPresets(state);
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
                    pawn.drafter.Drafted = true;
                    Check(state.Validate(new[] { edit }) != null, "Draft rejected when pawn becomes drafted after preview");
                    pawn.drafter.Drafted = false;
                    Check(state.Validate(new Assignment[] { null }) != null, "Null draft entry rejected safely");
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
                    AuditAllWorkTypes(state);
                    AuditNumberedConsent(state);
                    AuditUtilityBudget(state);
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
                    AuditPresetValidation(state);
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
                    var originalRules = state.Rules; bool originalPolicy = state.BalancedPolicy;
                    state.Rules = new List<WorkRule>(); Presets.Balanced(state);
                    var beforeBalanced = state.Workers.SelectMany(p => WorkAccess.Works().Where(w => WorkAccess.Capable(p,w)).Select(w => p.ThingID+"|"+w.defName+"="+p.workSettings.GetPriority(w))).ToList();
                    var balancedPlan = state.Plan();
                    Check(state.Validate(balancedPlan) == null, "Balanced primary/backup plan validates against live colony");
                    Check(beforeBalanced.SequenceEqual(state.Workers.SelectMany(p => WorkAccess.Works().Where(w => WorkAccess.Capable(p,w)).Select(w => p.ThingID+"|"+w.defName+"="+p.workSettings.GetPriority(w)))), "Balanced preview does not mutate real priorities");
                    int Projected(Pawn p, WorkTypeDef w) => balancedPlan.FirstOrDefault(c=>c.Pawn==p&&c.Work==w)?.After ?? p.workSettings.GetPriority(w);
                    foreach(var essentialName in new[]{"Doctor","Cooking","Growing","Construction"})
                    {
                        var essential=DefDatabase<WorkTypeDef>.GetNamed(essentialName);
                        var capable=state.Workers.Where(p=>state.Managed(p)&&WorkAccess.Available(p)&&WorkAccess.Capable(p,essential)&&!state.IsLocked(p,essential)).ToList();
                        if(capable.Count>0)Check(capable.Any(p=>Projected(p,essential)>0&&Projected(p,essential)<=3),"Balanced essential coverage: "+essentialName);
                    }
                    Check(balancedPlan.All(c=>c.Work.defName!="Hunting"&&c.Work.defName!="DarkStudy"),"Balanced preset preserves manual hunting and dark study");
                    Check(balancedPlan.All(c=>!state.IsLocked(c.Pawn,c.Work)&&state.Managed(c.Pawn)),"Balanced preset preserves locks and excluded pawns");
                    state.Rules=originalRules;state.BalancedPolicy=originalPolicy;
                    state.PresetName="Persistence check";state.PresetDirty=true;state.Rule(work).IgnoreMainJobLimit=true;
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
                    Check(state.PresetName=="Persistence check"&&state.PresetDirty&&state.Rule(work).IgnoreMainJobLimit,"Preset name dirty flag and focus rule survive save and reload");
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
                    var cachedNeeds=state.Snapshot(true);
                    state.Daily=true;state.NextDailyTick=tick+60000;
                    state.ProcessAutomation(tick+2500);
                    Check(ReferenceEquals(cachedNeeds,state.Snapshot()),"Waiting daily automation does not rescan or invalidate colony needs");
                    state.Daily=false;
                    if (otherTimer != null && originalOtherTimer.HasValue) otherTimer.SetValue(otherComponent, originalOtherTimer.Value);
                    Check(DefDatabase<MainButtonDef>.GetNamed("Work").TabWindow is MainTabWindow_Priorities, "mulld is attached to the vanilla Work tab");
                    Check(DefDatabase<MainButtonDef>.GetNamedSilentFail("MulldPriorities") == null, "Standalone mulld tab removed");
                    Find.MainTabsRoot.SetCurrentTab(DefDatabase<MainButtonDef>.GetNamed("Work"));
                    Find.TickManager.Pause();
                    AuditPresetInterface(state,(MainTabWindow_Priorities)DefDatabase<MainButtonDef>.GetNamed("Work").TabWindow);
                    frames = 0;
                }
                else if (phase == 3)
                {
                    frames++;
                    var window = (MainTabWindow_Priorities)DefDatabase<MainButtonDef>.GetNamed("Work").TabWindow;
                    var work = DefDatabase<WorkTypeDef>.GetNamed(workName);
                    if (frames == 30) ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "mulld-main-interface.png"));
                    if (frames == 45) window.SmokeView("Work", work);
                    if (frames == 75) ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "mulld-editor.png"));
                    if (frames == 90) window.SmokeView("Colonists");
                    if (frames == 120) ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "mulld-colonists.png"));
                    if (frames == 130) window.SmokeView("Rules");
                    if (frames == 140) ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "mulld-rules.png"));
                    if (frames == 145) window.SmokeView("Settings");
                    if (frames == 165) ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "mulld-settings.png"));
                    if (frames == 180)
                    {
                        var before = state.Workers.SelectMany(p=>WorkAccess.Works().Where(w=>WorkAccess.Capable(p,w)).Select(w=>p.ThingID+"|"+w.defName+"="+p.workSettings.GetPriority(w))).ToList();
                        window.SmokeView("Work", work, true);
                        Check(window.SmokeDraftCount>0,"Balanced changes staged for native-grid review");
                        Check(before.SequenceEqual(state.Workers.SelectMany(p=>WorkAccess.Works().Where(w=>WorkAccess.Capable(p,w)).Select(w=>p.ThingID+"|"+w.defName+"="+p.workSettings.GetPriority(w)))),"Opening grid review leaves real priorities untouched");
                    }
                    if (frames == 210) ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "mulld-review.png"));
                    if (frames == 225)
                    {
                        Check(window.SmokeApply()==null,"Reviewed balanced plan applies from the integrated panel");
                        var activeWorkers=state.Workers.Where(p=>state.Managed(p)&&WorkAccess.Available(p)).ToList();
                        Check(activeWorkers.All(p=>WorkAccess.Works().Any(w=>WorkAccess.MainWork(w)&&WorkAccess.Capable(p,w)&&p.workSettings.GetPriority(w) is 1 or 2)),"Applied balanced plan gives each available managed pawn a primary role");
                        foreach(var name in new[]{"Doctor","Cooking","Growing","Construction"})
                        {
                            var w=DefDatabase<WorkTypeDef>.GetNamed(name);
                            var eligible=activeWorkers.Where(p=>WorkAccess.Capable(p,w)&&!state.IsLocked(p,w)).ToList();
                            if(eligible.Count>0)Check(eligible.Any(p=>p.workSettings.GetPriority(w)>0),"Applied balanced coverage: "+name);
                        }
                    }
                    if(frames==250)ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath,"mulld-balanced-applied.png"));
                    if(frames==265)window.SmokeCollapse();
                    if(frames==290)ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath,"mulld-collapsed.png"));
                    if(frames==310){window.SmokeToggle();Find.WindowStack.Add(new PresetSaveDialog(state.PresetName,(name,overwrite)=>window.SmokeSavePreset(name,overwrite)));}
                    if(frames==340)ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath,"mulld-save-preset.png"));
                    if(frames==350)Find.WindowStack.WindowOfType<PresetSaveDialog>().SmokeSubmit("UI preset");
                    if(frames==375)ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath,"mulld-overwrite-preset.png"));
                    if(frames==390){Find.WindowStack.WindowOfType<PresetOverwriteDialog>().SmokeConfirm();}
                    if (frames == 420)
                    {
                        var errors = Log.Messages.Where(m => m.type == LogMessageType.Error).ToList();
                        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "game-messages.txt"), string.Join(Environment.NewLine, Log.Messages.Select(m => m.type + ": " + m.text + Environment.NewLine + m.StackTrace)));
                        Check(errors.Count == 0, "No game errors during load, reload and all integrated interface views");
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
        private static void AuditAllWorkTypes(PriorityMap state)
        {
            Find.TickManager.Pause();
            var savedRules=state.Rules;bool savedPolicy=state.BalancedPolicy;
            var originals=state.Workers.SelectMany(p=>WorkAccess.Works().Where(w=>WorkAccess.Capable(p,w)).Select(w=>new Assignment(p,w,p.workSettings.GetPriority(w),p.workSettings.GetPriority(w),"Audit restoration"))).ToList();
            try
            {
                foreach(var w in WorkAccess.Works())
                {
                    var eligible=state.Workers.Where(p=>state.Managed(p)&&WorkAccess.Available(p)&&WorkAccess.Capable(p,w)&&!state.IsLocked(p,w)).ToList();
                    if(eligible.Count==0){File.AppendAllText(Results,"SKIP No eligible worker for work audit: "+w.defName+Environment.NewLine);continue;}
                    foreach(bool balanced in new[]{false,true})
                    {
                        foreach(var p in eligible)p.workSettings.SetPriority(w,4);
                        state.BalancedPolicy=balanced;
                        var rule=new WorkRule{Work=w,Enabled=true,Mode="Everyone",Priority=3,AdultsOnly=false};
                        state.Rules=new List<WorkRule>{rule};
                        var plan=state.Plan();string tag=(balanced?"Balanced":"Legacy")+" "+w.defName;
                        Check(plan.Count==eligible.Count&&plan.All(c=>c.Work==w&&c.After==3),tag+": every eligible worker planned");
                        Check(state.Apply(plan,false)==null&&eligible.All(p=>p.workSettings.GetPriority(w)==3),tag+": real assignments apply");
                        state.UndoLast();
                        Check(eligible.All(p=>p.workSettings.GetPriority(w)==4),tag+": undo restores every assignment");
                        rule.Mode="Off";
                        plan=state.Plan();
                        Check(plan.Count==eligible.Count&&plan.All(c=>c.After==0)&&state.Apply(plan,false)==null&&eligible.All(p=>p.workSettings.GetPriority(w)==0),tag+": Off applies without fallback re-enabling work");
                        state.UndoLast();
                    }
                }
            }
            finally
            {
                foreach(var original in originals)original.Pawn.workSettings.SetPriority(original.Work,original.Before);
                state.Rules=savedRules;state.BalancedPolicy=savedPolicy;state.Undo.Clear();state.UndoBoosts.Clear();state.Invalidate();
            }
        }
        private static void AuditPresetValidation(PriorityMap state)
        {
            string path=Path.Combine(GenFilePaths.ConfigFolderPath,"mulldPriorities-preset.xml");
            string original=File.ReadAllText(path);
            var saved=state.Rules;bool policy=state.BalancedPolicy;
            try
            {
                var xml=System.Xml.Linq.XElement.Parse(original);
                xml.SetAttributeValue("balanced","invalid");File.WriteAllText(path,xml.ToString());
                Check(!Presets.Import(state).StartsWith("Imported")&&ReferenceEquals(saved,state.Rules)&&state.BalancedPolicy==policy,"Invalid policy import leaves rules and policy untouched");
                xml=System.Xml.Linq.XElement.Parse(original);xml.Elements("rule").First().SetAttributeValue("fallback",2);File.WriteAllText(path,xml.ToString());
                Check(!Presets.Import(state).StartsWith("Imported")&&ReferenceEquals(saved,state.Rules),"Imported primary-priority fallback rejected without replacing rules");
                xml=System.Xml.Linq.XElement.Parse(original);xml.SetAttributeValue("version",2);xml.SetAttributeValue("balanced",true);
                var first=xml.Elements("rule").First();first.SetAttributeValue("backups",2);first.SetAttributeValue("backupPriority",4);first.SetAttributeValue("fallback",4);first.SetAttributeValue("essential",true);File.WriteAllText(path,xml.ToString());
                Check(Presets.Import(state).StartsWith("Imported")&&state.BalancedPolicy&&state.Rules[0].Backups==2&&state.Rules[0].BackupPriority==4&&state.Rules[0].FallbackPriority==4&&state.Rules[0].Essential,"Version-2 preset preserves balanced backup and essential settings");
                xml=System.Xml.Linq.XElement.Parse(original);xml.SetAttributeValue("version",1);xml.Attribute("balanced")?.Remove();
                foreach(var r in xml.Elements("rule"))foreach(var key in new[]{"backups","backupPriority","fallback","essential"})r.Attribute(key)?.Remove();
                File.WriteAllText(path,xml.ToString());
                Check(Presets.Import(state).StartsWith("Imported")&&!state.BalancedPolicy&&state.Rules.All(r=>r.Backups==0&&r.FallbackPriority==0),"Legacy version-1 preset imports with original semantics");
            }
            finally{File.WriteAllText(path,original);state.Rules=saved;state.BalancedPolicy=policy;}
        }
        private static void AuditNumberedConsent(PriorityMap state)
        {
            var pawn=state.Workers.First(p=>state.Managed(p)&&WorkAccess.Available(p));
            var work=WorkAccess.Works().First(w=>WorkAccess.Capable(pawn,w)&&!state.IsLocked(pawn,w));
            bool numbered=Current.Game.playSettings.useWorkPriorities;
            int before=pawn.workSettings.GetPriority(work),after=before==2?3:2;
            try
            {
                Current.Game.playSettings.useWorkPriorities=false;
                int displayed=pawn.workSettings.GetPriority(work);
                var changes=new List<Assignment>{new Assignment(pawn,work,displayed,after,"Numbered consent audit")};
                Check(state.Apply(changes,false)!=null&&!Current.Game.playSettings.useWorkPriorities&&pawn.workSettings.GetPriority(work)==displayed,"Numbered priorities require explicit apply consent");
                Check(state.Apply(changes,true)==null&&Current.Game.playSettings.useWorkPriorities&&pawn.workSettings.GetPriority(work)==after,"Consented apply enables numbered priorities and applies the draft");
                state.UndoLast();
                Check(pawn.workSettings.GetPriority(work)==before,"Consented numbered apply remains undoable");
                Check(changes[0].Before==displayed,"Applying checkbox-mode draft does not mutate the caller's preview");
            }
            finally
            {
                pawn.workSettings.SetPriority(work,before);Current.Game.playSettings.useWorkPriorities=numbered;
                foreach(var m in Find.Maps)foreach(var p in m.mapPawns.AllPawns)p.workSettings?.Notify_UseWorkPrioritiesChanged();
                state.Undo.Clear();state.UndoBoosts.Clear();state.Invalidate();
            }
        }
        private static void AuditUtilityBudget(PriorityMap state)
        {
            var originals=state.Workers.SelectMany(p=>WorkAccess.Works().Where(w=>WorkAccess.Capable(p,w)).Select(w=>new Assignment(p,w,p.workSettings.GetPriority(w),0,"Audit restoration"))).ToList();
            var rules=state.Rules;bool policy=state.BalancedPolicy;int cap=state.MaxMainJobs;
            try
            {
                foreach(var c in originals)c.Pawn.workSettings.SetPriority(c.Work,WorkAccess.MainWork(c.Work)?4:1);
                var doctor=DefDatabase<WorkTypeDef>.GetNamed("Doctor");
                var workers=state.Workers.Where(p=>state.Managed(p)&&WorkAccess.Available(p)&&WorkAccess.Capable(p,doctor)&&!state.IsLocked(p,doctor)).ToList();
                state.Rules=new List<WorkRule>{new WorkRule{Work=doctor,Enabled=true,Mode="Everyone",Priority=2}};state.MaxMainJobs=1;
                foreach(bool balanced in new[]{false,true})
                {
                    state.BalancedPolicy=balanced;var plan=state.Plan();
                    Check(workers.Count>0&&workers.All(p=>plan.Any(c=>c.Pawn==p&&c.Work==doctor&&c.After==2)),(balanced?"Balanced":"Legacy")+": utility assignments do not consume specialist budget");
                }
            }
            finally{foreach(var c in originals)c.Pawn.workSettings.SetPriority(c.Work,c.Before);state.Rules=rules;state.BalancedPolicy=policy;state.MaxMainJobs=cap;state.Invalidate();}
        }
        private static string Priorities(PriorityMap state) => string.Join(";",state.Workers.SelectMany(p=>WorkAccess.Works().Where(w=>WorkAccess.Capable(p,w)).Select(w=>p.ThingID+"|"+w.defName+"="+p.workSettings.GetPriority(w))));
        private static void AuditNamedPresets(PriorityMap state)
        {
            var oldRules=state.Rules;bool policy=state.BalancedPolicy,daily=state.Daily,dirty=state.PresetDirty;int cap=state.MaxMainJobs;string name=state.PresetName;
            var locks=state.Locks.ToList();var excluded=state.Excluded.ToList();string before=Priorities(state);
            string path=Path.Combine(GenFilePaths.ConfigFolderPath,"mulldPriorities-presets.xml"),backup=File.Exists(path)?File.ReadAllText(path):null;
            state.Rules=new List<WorkRule>();
            try
            {
                Check(PresetCatalog.Load(state,"Balanced")==null&&!state.PresetDirty&&state.PresetName=="Balanced","Balanced preset loads rules without applying priorities");
                Check(PresetCatalog.Save(state,"Colony routine",false)==null,"Named preset saves current rules");
                var cook=state.Rule(DefDatabase<WorkTypeDef>.GetNamed("Cooking"));cook.Priority=3;state.MaxMainJobs=1;
                Check(PresetCatalog.Save(state,"COLONY ROUTINE",false)!=null,"Preset overwrite requires explicit confirmation");
                Check(PresetCatalog.Load(state,"Colony routine")==null&&state.Rule(cook.Work).Priority==2,"Unconfirmed overwrite preserves original rules");
                state.Rule(cook.Work).Priority=3;state.MaxMainJobs=1;
                Check(PresetCatalog.Save(state,"colony routine",true)==null&&PresetCatalog.Load(state,"COLONY ROUTINE")==null&&state.Rule(cook.Work).Priority==3&&state.MaxMainJobs==1,"Confirmed overwrite replaces named rules and job limit");
                PresetCatalog.Store.Reload();
                Check(PresetCatalog.Load(state,"Colony routine")==null&&state.Rule(cook.Work).Priority==3,"Named preset survives library reload");
                Check(PresetCatalog.Load(state,"Balanced")==null,"Factory Balanced remains selectable");
                state.Rule(cook.Work).Priority=4;
                Check(PresetCatalog.Save(state,"Balanced",true)==null&&PresetCatalog.Load(state,"Balanced")==null&&state.Rule(cook.Work).Priority==4,"Built-in name can be locally overridden after confirmation");
                Check(PresetCatalog.Load(state,"Balanced",true)==null&&state.Rule(cook.Work).Priority==2&&state.PresetDirty&&PresetCatalog.Store.Contains("Balanced"),"Factory defaults remain available without deleting a custom override");
                foreach(var focus in new[]{("Clean","Cleaning"),("Harvest","Growing"),("Haul","Hauling")})
                {
                    Check(PresetCatalog.Load(state,focus.Item1)==null,"Focus preset loads: "+focus.Item1);
                    var work=DefDatabase<WorkTypeDef>.GetNamed(focus.Item2);var rule=state.Rule(work);state.MaxMainJobs=1;
                    Check(rule.Mode=="Everyone"&&rule.Priority==1&&rule.IgnoreMainJobLimit,"Focus configures everyone at priority 1: "+focus.Item1);
                    var plan=state.Plan();var eligible=state.Workers.Where(p=>state.Managed(p)&&WorkAccess.Available(p)&&WorkAccess.Capable(p,work)&&!state.IsLocked(p,work)).ToList();
                    Check(eligible.Count>0&&eligible.All(p=>(plan.FirstOrDefault(c=>c.Pawn==p&&c.Work==work)?.After??p.workSettings.GetPriority(work))==1),"Focus priority survives full primary-role budget: "+focus.Item1);
                }
                var clean=DefDatabase<WorkTypeDef>.GetNamed("Cleaning");var pawn=state.Workers.First(p=>WorkAccess.Capable(p,clean));state.SetLock(pawn,clean,true);state.Excluded.Add(pawn.ThingID);
                PresetCatalog.Load(state,"Clean");
                Check(state.Plan().All(c=>c.Pawn!=pawn)&&state.IsLocked(pawn,clean),"Focus presets preserve excluded colonists and locked priorities");
                Check(PresetCatalog.Save(state,"Cleaning routine",false)==null&&PresetCatalog.Load(state,"Cleaning routine")==null&&state.Rule(clean).IgnoreMainJobLimit,"Version-3 named presets preserve focus exemption");
                var unknown=Presets.Capture(state);var extra=new System.Xml.Linq.XElement(unknown.Elements("rule").First());extra.SetAttributeValue("work","Mulld_MissingWorkForTest");unknown.Add(extra);
                Check(PresetCatalog.Store.Save("Missing mod",unknown,false)==null&&PresetCatalog.Load(state,"Missing mod")==null&&state.Rules.All(r=>r.Work!=null),"Absent mod jobs are skipped when loading a named preset");
                PresetCatalog.Save(state,"Another preset",false);PresetCatalog.Store.TryGet("Missing mod",out var retained);
                Check(retained.Elements("rule").Any(r=>(string)r.Attribute("work")=="Mulld_MissingWorkForTest"),"Saving another preset preserves unavailable jobs in stored definitions");
                Check(Priorities(state)==before&&state.Daily==daily,"Loading and saving presets never mutate live priorities or daily automation");
            }
            finally
            {
                state.Rules=oldRules;state.BalancedPolicy=policy;state.MaxMainJobs=cap;state.PresetName=name;state.PresetDirty=dirty;state.Locks=locks;state.Excluded=excluded;state.Invalidate();
                if(backup==null)File.Delete(path);else File.WriteAllText(path,backup);PresetCatalog.Store.Reload();
            }
        }
        private static void AuditPresetInterface(PriorityMap state,MainTabWindow_Priorities window)
        {
            string before=Priorities(state);
            Check(window.SmokeLoadPreset("Balanced")==null,"Icon-group preset selection loads without applying");
            window.SmokePreview();var change=window.SmokeFirstChange;
            Check(change!=null&&window.Reviewing&&window.TryPreview(change.Pawn,change.Work,out var shown)&&shown.After==change.After,"Proposed-number grid reads staged values only");
            window.SmokeInspect(change);
            Check(window.SmokeInspection.Contains(WorkAccess.PriorityLabel(change.Before)+" → "+WorkAccess.PriorityLabel(change.After))&&Priorities(state)==before,"Selecting a changed cell exposes current and proposed values without mutation");
            int draft=window.SmokeDraftCount;window.SmokeToggle();
            Check(!window.Expanded&&!window.Reviewing&&window.SmokeDraftCount==draft,"Segmented collapse hides preview without discarding draft");
            window.SmokeToggle();
            Check(window.Expanded&&window.Reviewing&&window.SmokeDraftCount==draft,"Segmented expand restores staged proposed-number view");
            window.SmokeRuleChanged();
            Check(window.SmokeDraftCount==0&&!window.Reviewing&&state.PresetDirty,"Editing rules invalidates draft and marks preset modified");
            Check(window.SmokeSavePreset("UI preset",false)==null&&!state.PresetDirty,"Save icon flow records custom name without applying");
            var capturedSave=window.SmokeSaveAction();int capturePriority=state.Rule(change.Work).Priority;state.Rule(change.Work).Priority=capturePriority==1?2:1;
            Check(capturedSave("Stale dialog preset",false)!=null&&!PresetCatalog.Store.Contains("Stale dialog preset"),"Save dialog rejects a rule set changed after opening");
            state.Rule(change.Work).Priority=capturePriority;
            int originalPriority=state.Rule(change.Work).Priority;int replacementPriority=originalPriority==2?3:2;state.Rule(change.Work).Priority=replacementPriority;
            var dialog=new PresetSaveDialog("UI preset",(name,overwrite)=>window.SmokeSavePreset(name,overwrite));Find.WindowStack.Add(dialog);dialog.SmokeSubmit("UI preset");
            var box=Find.WindowStack.Windows.OfType<PresetOverwriteDialog>().Last();
            Check(box.PresetName=="UI preset","Save dialog opens a native overwrite confirmation");
            PresetCatalog.Store.TryGet("UI preset",out var original);
            Check((int?)original.Elements("rule").First(r=>(string)r.Attribute("work")==change.Work.defName).Attribute("priority")==originalPriority,"Overwrite confirmation does not commit before approval");
            box.Close();PresetCatalog.Store.TryGet("UI preset",out var canceled);
            Check(System.Xml.Linq.XNode.DeepEquals(original,canceled),"Canceling overwrite preserves saved rules");
            dialog.SmokeSubmit("UI preset");box=Find.WindowStack.Windows.OfType<PresetOverwriteDialog>().Last();
            box.SmokeConfirm();PresetCatalog.Store.TryGet("UI preset",out var replaced);
            Check((int?)replaced.Elements("rule").First(r=>(string)r.Attribute("work")==change.Work.defName).Attribute("priority")==replacementPriority,"Native overwrite confirmation saves replacement rules");
            Check(Priorities(state)==before,"Preset menu save dialog and inspection leave assignments unchanged");
            window.SmokeLoadPreset("Balanced");
        }
        private static void AuditDiagnostics(PriorityMap state)
        {
            Check(!new MulldSettings().PerformanceDiagnostics,"Performance diagnostics default off");
            MulldMod.Settings.PerformanceDiagnostics=false;PerformanceMetrics.Enabled=false;PerformanceMetrics.Reset();
            state.Snapshot(true);state.Plan();
            Check(PerformanceMetrics.Get(PerformanceOperation.NeedsScan).Samples==0&&PerformanceMetrics.Get(PerformanceOperation.Plan).Samples==0,"Disabled diagnostics collect no scan or plan samples");
            PerformanceMetrics.Enabled=true;PerformanceMetrics.Reset();state.Snapshot(true);state.Snapshot();state.Plan();
            Check(PerformanceMetrics.Get(PerformanceOperation.NeedsScan).Samples==1&&PerformanceMetrics.Get(PerformanceOperation.Plan).Samples==1&&PerformanceMetrics.SnapshotHits==1,"Enabled diagnostics measure real scans plans and cache hits");
            MulldMod.Settings.PerformanceDiagnostics=true;MulldMod.Instance.WriteSettings();
            string path=Directory.GetFiles(GenFilePaths.ConfigFolderPath,"Mod_*_MulldMod.xml").Single();
            Check((bool?)System.Xml.Linq.XElement.Load(path).Descendants("performanceDiagnostics").FirstOrDefault()==true,"Diagnostics toggle persists in the isolated Mod Settings file");
            MulldMod.Settings.PerformanceDiagnostics=false;PerformanceMetrics.Enabled=false;MulldMod.Instance.WriteSettings();
            Check(!((bool?)System.Xml.Linq.XElement.Load(path).Descendants("performanceDiagnostics").FirstOrDefault()??false),"Diagnostics disabled setting saves as false/default");
            state.Plan();Check(PerformanceMetrics.Get(PerformanceOperation.Plan).Samples==1,"Turning diagnostics off stops further recording");
            PerformanceMetrics.Reset();
        }
    }
}
