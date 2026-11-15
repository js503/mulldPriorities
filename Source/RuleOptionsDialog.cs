using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace Mulld.Priorities
{
    // Edits rules only; the parent invalidates its draft after each change.
    internal sealed class RuleOptionsDialog : Window
    {
        private readonly WorkRule rule;
        private readonly Action changed;
        private readonly Map map;
        public override Vector2 InitialSize => new Vector2(480, 340);
        internal RuleOptionsDialog(WorkRule rule, Action changed)
        {
            this.rule = rule; this.changed = changed; map = Find.CurrentMap;
            doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; forcePause = true;
        }
        public override void DoWindowContents(Rect rect)
        {
            if (Find.CurrentMap != map) { Close(); return; }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width, 32), WorkAccess.Label(rule.Work) + " options");
            Text.Font = GameFont.Small;
            var list = new Listing_Standard(); list.Begin(new Rect(0, 42, rect.width, 220));
            if (list.ButtonText("Minimum skill: " + rule.MinSkill))
                Find.WindowStack.Add(new FloatMenu(Enumerable.Range(0, 21).Select(value => new FloatMenuOption(value.ToString(), () => { rule.MinSkill = value; changed(); })).ToList()));
            if (list.ButtonText("Backup priority: " + rule.BackupPriority))
                Find.WindowStack.Add(new FloatMenu(Enumerable.Range(1, 4).Select(value => new FloatMenuOption(value.ToString(), () => { rule.BackupPriority = value; changed(); })).ToList()));
            bool adults = rule.AdultsOnly, passions = rule.FavorPassion, focus = rule.IgnoreMainJobLimit;
            list.CheckboxLabeled("Adults only", ref adults);
            list.CheckboxLabeled("Prefer passions", ref passions);
            list.CheckboxLabeled("Allow extra primary jobs (focus)", ref focus);
            if (adults != rule.AdultsOnly || passions != rule.FavorPassion || focus != rule.IgnoreMainJobLimit)
            { rule.AdultsOnly = adults; rule.FavorPassion = passions; rule.IgnoreMainJobLimit = focus; changed(); }
            list.End();
        }
    }
}
