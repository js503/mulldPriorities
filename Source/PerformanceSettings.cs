using System.Diagnostics;
using UnityEngine;
using Verse;

namespace Mulld.Priorities
{
    public sealed class MulldSettings : ModSettings
    {
        public bool PerformanceDiagnostics;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref PerformanceDiagnostics, "performanceDiagnostics", false);
        }
    }

    public static class PerformancePanel
    {
        private static readonly string[] names = { "Needs scan", "Priority plan", "Draft validation", "Apply batch", "Automation run", "Needs / controls drawing" };
        private static readonly string[] headers = { "Calls", "Avg ms", "Worst ms", "Avg KiB", "Worst KiB" };
        private static Vector2 scroll;
        private static readonly string[,] cells = new string[(int)PerformanceOperation.Count, 5];
        private static long refreshed;
        private static string cacheText;
        public static void Draw(Rect area)
        {
            var font = Text.Font; var anchor = Text.Anchor; var color = GUI.color; bool wrap = Text.WordWrap;
            Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; GUI.color = Color.white; Text.WordWrap = true;
            var view = new Rect(0, 0, area.width - 20, System.Math.Max(area.height, 520));
            Widgets.BeginScrollView(area, ref scroll, view);
            try { DrawContents(view); }
            finally { Widgets.EndScrollView(); Text.Font = font; Text.Anchor = anchor; GUI.color = color; Text.WordWrap = wrap; }
        }
        private static void DrawContents(Rect area)
        {
            var settings = MulldMod.Settings;
            bool enabled = settings.PerformanceDiagnostics;
            Widgets.CheckboxLabeled(new Rect(area.x, area.y, area.width, 30), "Performance diagnostics (off by default)", ref enabled);
            if (enabled != settings.PerformanceDiagnostics)
            {
                settings.PerformanceDiagnostics = enabled;
                PerformanceMetrics.Enabled = enabled;
                refreshed = 0;
            }
            Widgets.Label(new Rect(area.x, area.y + 40, area.width, 52), "Measures mulld's elapsed time and managed allocations on the calling thread. Nested operations overlap; do not add their times. No per-mod CPU percentage or RAM estimate.");
            if (!enabled)
            {
                Widgets.Label(new Rect(area.x, area.y + 104, area.width, 60), "Disabled: no timestamps, allocation sampling, history or background monitoring. Enable to collect measurements while you use the colony and Work tab.");
                return;
            }
            long now = Stopwatch.GetTimestamp();
            if (refreshed == 0 || now - refreshed >= Stopwatch.Frequency)
            {
                refreshed = now;
                for (int i = 0; i < names.Length; i++)
                {
                    var c = PerformanceMetrics.Get((PerformanceOperation)i);
                    cells[i, 0] = c.Samples.ToString(); cells[i, 1] = c.AverageMs.ToString("0.000"); cells[i, 2] = c.WorstMs.ToString("0.000");
                    cells[i, 3] = (c.AverageBytes / 1024d).ToString("0.0"); cells[i, 4] = (c.MaxBytes / 1024d).ToString("0.0");
                }
                cacheText = "Cached needs reads: " + PerformanceMetrics.SnapshotHits + ". Counters since reset; display refreshes once per second.";
            }
            float labelWidth = area.width * .35f, col = (area.width - labelWidth) / 5f;
            float y = area.y + 105;
            Widgets.Label(new Rect(area.x, y, labelWidth, 26), "Operation");
            for (int j = 0; j < headers.Length; j++) Widgets.Label(new Rect(area.x + labelWidth + j * col, y, col, 26), headers[j]);
            y += 30;
            for (int i = 0; i < names.Length; i++, y += 28)
            {
                Widgets.Label(new Rect(area.x, y, labelWidth, 26), names[i]);
                for (int j = 0; j < 5; j++) Widgets.Label(new Rect(area.x + labelWidth + j * col, y, col, 26), cells[i, j]);
            }
            Widgets.Label(new Rect(area.x, y + 6, area.width, 50), cacheText);
            if (Widgets.ButtonText(new Rect(area.x, y + 62, 160, 30), "Reset counters")) { PerformanceMetrics.Reset(); refreshed = 0; }
            Widgets.Label(new Rect(area.x, y + 102, area.width, 60), "Panel timing covers mulld's extension on Repaint, excluding the native work table. Allocations are cumulative bytes allocated, not retained memory. Profiling itself adds some overhead.");
        }
    }
}
