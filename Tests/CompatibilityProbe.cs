using System;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mulld.CompatibilityProbe
{
    public sealed class ProbeMod : Mod
    {
        public ProbeMod(ModContentPack content) : base(content)
        {
            if (GenCommandLine.CommandLineArgPassed("mulldloadtest"))
                LongEventHandler.ExecuteWhenFinished(() => GameDataSaveLoader.LoadGame("mulld-compat-copy"));
        }
    }
    public sealed class ProbeComponent : GameComponent
    {
        private int frames;
        public ProbeComponent(Game game) { }
        public override void GameComponentUpdate()
        {
            if (!GenCommandLine.CommandLineArgPassed("mulldbaseline") || Find.CurrentMap == null || ++frames != 90) return;
            string folder = GenFilePaths.SaveDataFolderPath;
            File.WriteAllText(Path.Combine(folder, "loaded-mods.txt"), string.Join(Environment.NewLine, LoadedModManager.RunningMods.Select(m => m.PackageId + " | " + m.Name)));
            File.WriteAllText(Path.Combine(folder, "game-messages.txt"), string.Join(Environment.NewLine, Log.Messages.Select(m => m.type + ": " + m.text + Environment.NewLine + m.StackTrace)));
            File.WriteAllText(Path.Combine(folder, "baseline-complete.txt"), "Baseline completed without mulld Priorities. Errors recorded: " + Log.Messages.Count(m => m.type == LogMessageType.Error));
            Application.Quit();
        }
    }
}
