using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mulld.Priorities
{
    public sealed class MulldMod : Mod
    {
        public static MulldMod Instance { get; private set; }
        public static MulldSettings Settings { get; private set; }
        public MulldMod(ModContentPack content) : base(content)
        {
            Instance = this; Settings = GetSettings<MulldSettings>();
            PerformanceMetrics.Enabled = Settings.PerformanceDiagnostics;
            new Harmony("mulld.priorities").PatchAll(typeof(MulldMod).Assembly);
        }
        public override string SettingsCategory() => "mulld Priorities";
        public override void DoSettingsWindowContents(Rect inRect) => PerformancePanel.Draw(inRect);
    }
    [HarmonyPatch(typeof(PawnColumnWorker_WorkPriority), nameof(PawnColumnWorker_WorkPriority.DoCell))]
    public static class WorkGridPreview
    {
        private static readonly string[] values = { "–", "1", "2", "3", "4" };
        private static readonly Color[] colors = { new(.7f,.73f,.7f), new(.35f,.90f,.25f), new(.95f,.88f,.45f), new(.83f,.7f,.43f), new(.75f,.77f,.73f) };
        public static bool Prefix(PawnColumnWorker_WorkPriority __instance, Rect rect, Pawn pawn, PawnTable table)
        {
            var window=MainTabWindow_Priorities.Drawing;
            if(window==null||!window.Reviewing)return true;
            var work=__instance.def.workType;
            if(work==null||!window.TryPreview(pawn,work,out var change))
                return Event.current.type is EventType.Repaint or EventType.Layout;
            var oldColor=GUI.color;var oldFont=Text.Font;var oldAnchor=Text.Anchor;bool oldWrap=Text.WordWrap;
            try
            {
                var box=new Rect(rect.center.x-12,rect.center.y-13,24,26);
                Widgets.DrawBoxSolidWithOutline(box,new Color(.19f,.18f,.14f),window.IsInspected(change)?new Color(.98f,.85f,.58f):new Color(.72f,.59f,.36f));
                GUI.color=colors[change.After];Text.Font=GameFont.Medium;Text.Anchor=TextAnchor.MiddleCenter;Text.WordWrap=false;
                Widgets.Label(new Rect(box.x-3,box.y-1,box.width+6,box.height+2),values[change.After]);
                TooltipHandler.TipRegion(rect,window.PreviewTip(change));
                if(Widgets.ButtonInvisible(rect))window.Inspect(change);
            }
            finally{GUI.color=oldColor;Text.Font=oldFont;Text.Anchor=oldAnchor;Text.WordWrap=oldWrap;}
            return false;
        }
    }
    [HarmonyPatch(typeof(PawnColumnWorker_WorkPriority), "HeaderClicked")]
    public static class WorkGridReviewHeader
    {
        public static bool Prefix() => MainTabWindow_Priorities.Drawing?.Reviewing != true;
    }
}
