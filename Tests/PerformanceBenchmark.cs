using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mulld.Priorities
{
    // Diagnostic builds only. All setup and mutations stay in the isolated profile.
    public static class PerformanceBenchmark
    {
        private static int phase, frames;
        private static readonly Dictionary<string, object> results = new Dictionary<string, object>();
        private static string Root => GenFilePaths.SaveDataFolderPath;
        private static void Progress(string text) => File.AppendAllText(Path.Combine(Root,"benchmark-progress.txt"),text+Environment.NewLine);
        public static void Update(PriorityMap state)
        {
            if (phase == 99) return;
            try
            {
                if (phase == 0)
                {
                    phase = 1; Find.TickManager.Pause();
                    File.WriteAllText(Path.Combine(Root,"benchmark-progress.txt"),"Preparing 100-colonist benchmark"+Environment.NewLine);
                    if (MulldMod.Settings.PerformanceDiagnostics || PerformanceMetrics.Enabled) throw new Exception("Diagnostics must default off in this test profile.");
                    state.Daily=false;state.Excluded.Clear();state.Locks.Clear();state.Boosts.Clear();
                    var other=state.map.components.FirstOrDefault(c=>c.GetType().FullName=="AutoPriorities.Core.MapSpecificData");
                    other?.GetType().GetProperty("RunOnTimer")?.SetValue(other,false);
                    bool setup=state.Workers.Count<100;
                    if(setup) Populate(state);
                    Current.Game.playSettings.useWorkPriorities=true;
                    Presets.Balanced(state);
                    foreach(var p in state.Workers)foreach(var w in WorkAccess.Works())if(WorkAccess.Capable(p,w))p.workSettings.SetPriority(w,4);
                    state.Invalidate();
                    GameDataSaveLoader.SaveGame("mulld-large-benchmark");
                    results["fixture"]=new { workers=state.Workers.Count, things=state.map.listerThings.AllThings.Count, haulables=state.map.listerHaulables.ThingsPotentiallyNeedingHauling().Count(), readyCrops=state.Snapshot(true).ReadyCrops, workTypes=WorkAccess.Works().Count, mods=LoadedModManager.RunningMods.Count(), version=typeof(PriorityMap).Assembly.GetName().Version.ToString(), runtime=Environment.Version.ToString(), logicalProcessors=Environment.ProcessorCount };
                    File.WriteAllText(Path.Combine(Root,"loaded-mods.txt"),string.Join(Environment.NewLine,LoadedModManager.RunningMods.Select(m=>m.PackageId+" | "+m.Name)));
                    Progress("Fixture ready. Measuring warmed operations with diagnostics off and on.");
                    RunOperations(state,false); RunOperations(state,true);
                    PerformanceMetrics.Enabled=true;PerformanceMetrics.Reset();
                    Find.MainTabsRoot.SetCurrentTab(DefDatabase<MainButtonDef>.GetNamed("Work"));
                    var window=(MainTabWindow_Priorities)DefDatabase<MainButtonDef>.GetNamed("Work").TabWindow;
                    window.SmokeView("Needs");frames=0;phase=2;
                }
                else if(phase>=2&&phase<=5)
                {
                    frames++;
                    if(frames==145) ScreenCapture.CaptureScreenshot(Path.Combine(Root,"benchmark-"+new[]{"Needs","Colonists","Rules","Review"}[phase-2]+".png"));
                    if(frames==30)PerformanceMetrics.Reset();
                    if(frames<150)return;
                    string[] pages={"Needs","Colonists","Rules","Review"};
                    var counter=PerformanceMetrics.Get(PerformanceOperation.PanelDraw);
                    if(counter.Samples<30)return;
                    results["panel"+pages[phase-2]]=new{samples=counter.Samples,averageMs=counter.AverageMs,worstMs=counter.WorstMs,averageBytes=counter.AverageBytes,worstBytes=counter.MaxBytes};
                    Progress("Measured "+pages[phase-2]+" panel: "+counter.AverageMs.ToString("0.000")+" ms / "+counter.AverageBytes.ToString("0")+" bytes per repaint.");
                    phase++;frames=0;
                    var window=(MainTabWindow_Priorities)DefDatabase<MainButtonDef>.GetNamed("Work").TabWindow;
                    if(phase==3)window.SmokeView("Colonists");
                    if(phase==4)window.SmokeView("Rules");
                    if(phase==5)window.SmokeView("Needs",DefDatabase<WorkTypeDef>.GetNamed("Doctor"),true);
                    if(phase==6)
                    {
                        MulldMod.Settings.PerformanceDiagnostics=true;MulldMod.Instance.WriteSettings();
                        state.Snapshot(true);var preview=state.Plan();state.Validate(preview);
                        Find.WindowStack.Add(new BenchmarkSettingsWindow());
                    }
                }
                else if(phase==6)
                {
                    frames++;
                    if(frames==30)ScreenCapture.CaptureScreenshot(Path.Combine(Root,"benchmark-diagnostics.png"));
                    if(frames<90)return;
                    MulldMod.Settings.PerformanceDiagnostics=false;PerformanceMetrics.Enabled=false;MulldMod.Instance.WriteSettings();
                    var errors=Log.Messages.Where(m=>m.type==LogMessageType.Error).ToList();
                    File.WriteAllText(Path.Combine(Root,"game-messages.txt"),string.Join(Environment.NewLine,Log.Messages.Select(m=>m.type+": "+m.text)));
                    if(errors.Count>0)throw new Exception("Game errors during benchmark: "+string.Join("; ",errors.Select(m=>m.text)));
                    File.WriteAllText(Path.Combine(Root,"benchmark-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
                    Progress("COMPLETE. Diagnostics switched off; no game errors.");phase=99;Application.Quit();
                }
            }
            catch(Exception e){Progress("FAIL "+e);phase=99;Log.Error("[mulld benchmark] "+e);Application.Quit(1);}
        }
        private static void Populate(PriorityMap state)
        {
            var cells=state.map.AllCells.Where(c=>c.Standable(state.map)&&!c.Fogged(state.map)&&c.GetEdifice(state.map)==null).ToList();
            int count=state.Workers.Count;
            while(count<100)
            {
                var pawn=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);
                GenSpawn.Spawn(pawn,cells[count*13%cells.Count],state.map);
                count++;if(count%10==0)Progress("Spawned "+count+" colonists.");
            }
            for(int i=0;i<4000;i++)
            {
                var item=ThingMaker.MakeThing(i<200?DefDatabase<ThingDef>.GetNamed("RawPotatoes"):ThingDefOf.Steel);item.stackCount=item.def.stackLimit;
                GenSpawn.Spawn(item,cells[i%cells.Count],state.map);
            }
            var soil=cells.Where(c=>c.GetPlant(state.map)==null&&c.GetTerrain(state.map).fertility>=.6f).Take(2000).ToList();
            foreach(var cell in soil){var crop=(Plant)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("Plant_Rice"),cell,state.map);crop.sown=true;crop.Growth=1;}
        }
        private static void RunOperations(PriorityMap state,bool enabled)
        {
            PerformanceMetrics.Enabled=enabled;PerformanceMetrics.Reset();
            var plan=state.Plan();if(state.Validate(plan)!=null||plan.Count==0)throw new Exception("Invalid/empty benchmark plan");
            results[(enabled?"on":"off")+"DraftChanges"]=plan.Count;
            void ApplyUndo(){string error=state.Apply(plan,false);if(error!=null)throw new Exception(error);state.UndoLast();}
            int tick=Find.TickManager.TicksGame;
            void Idle(){state.ProcessAutomation(tick);}
            void Daily(){state.Daily=true;state.NextDailyTick=tick;state.ProcessAutomation(tick);state.UndoLast();state.Daily=false;}
            string prefix=enabled?"on/":"off/";
            results[prefix+"NeedsScan"]=Measure(()=>state.Snapshot(true));
            results[prefix+"Plan"]=Measure(()=>state.Plan());
            results[prefix+"Validate"]=Measure(()=>{if(state.Validate(plan)!=null)throw new Exception("Invalid draft");});
            results[prefix+"ApplyUndo"]=Measure(ApplyUndo);
            results[prefix+"DailyApplyUndo"]=Measure(Daily);
            state.Daily=true;state.NextDailyTick=tick+60000;state.Snapshot(true);
            results[prefix+"WaitingAutomation"]=Measure(Idle,10000);
            results[prefix+"CachedNeeds"]=Measure(()=>state.Snapshot(),10000);
            state.Daily=false;Progress("Operations complete: diagnostics "+(enabled?"on":"off"));
        }
        private static object Measure(Action action,int batch=1)
        {
            for(int i=0;i<8;i++)action();
            var times=new double[64];var bytes=new double[64];int gen0=GC.CollectionCount(0);
            for(int i=0;i<times.Length;i++)
            {
                long allocated=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
                for(int j=0;j<batch;j++)action();
                times[i]=(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency/batch;
                bytes[i]=(GC.GetAllocatedBytesForCurrentThread()-allocated)/(double)batch;
            }
            Array.Sort(times);Array.Sort(bytes);
            return new{samples=times.Length,batch,medianMs=(times[31]+times[32])/2,p95Ms=times[60],worstMs=times[63],medianBytes=(bytes[31]+bytes[32])/2,gen0Collections=GC.CollectionCount(0)-gen0};
        }
        private sealed class BenchmarkSettingsWindow : Window
        {
            public override Vector2 InitialSize=>new Vector2(880,620);
            public BenchmarkSettingsWindow(){doCloseX=true;absorbInputAroundWindow=true;}
            public override void DoWindowContents(Rect rect)=>MulldMod.Instance.DoSettingsWindowContents(rect);
        }
    }
}
