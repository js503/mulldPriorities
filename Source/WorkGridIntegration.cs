using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mulld.Priorities
{
    public sealed class MulldMod : Mod
    {
        public MulldMod(ModContentPack content) : base(content)
        {
            new Harmony("mulld.priorities").PatchAll(typeof(MulldMod).Assembly);
        }
    }
    [HarmonyPatch(typeof(PawnColumnWorker_WorkPriority), nameof(PawnColumnWorker_WorkPriority.DoCell))]
    public static class WorkGridPreview
    {
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
                var box=new Rect(rect.center.x-14,rect.center.y-13,28,26);
                Widgets.DrawBoxSolid(box,new Color(.26f,.22f,.14f));
                Widgets.DrawBoxSolidWithOutline(box,new Color(.26f,.22f,.14f),new Color(.85f,.69f,.40f));
                GUI.color=new Color(.96f,.80f,.48f);Text.Font=GameFont.Tiny;Text.Anchor=TextAnchor.MiddleCenter;Text.WordWrap=false;
                Widgets.Label(new Rect(box.x-3,box.y,box.width+6,box.height),(change.Before==0?"–":change.Before.ToString())+"→"+(change.After==0?"–":change.After.ToString()));
                TooltipHandler.TipRegion(rect,pawn.LabelShort+" · "+WorkAccess.Label(work)+": "+WorkAccess.PriorityLabel(change.Before)+" → "+WorkAccess.PriorityLabel(change.After)+"\n"+change.Reason+"\nDraft only; actual priorities are unchanged.");
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
