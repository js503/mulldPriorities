using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Verse;

namespace Mulld.Priorities
{
    public static class Presets
    {
        private static string FilePath => Path.Combine(GenFilePaths.ConfigFolderPath, "mulldPriorities-preset.xml");
        public static void Balanced(PriorityMap state)
        {
            state.BalancedPolicy = true;
            var children = state.map.mapPawns.FreeColonistsSpawned.Any(p => !WorkAccess.Adult(p));
            var supported = new[] { "Firefighter", "Patient", "PatientBedRest", "BasicWorker", "Doctor", "Cooking", "Construction", "Growing", "Mining", "PlantCutting", "Handling", "Warden", "Smithing", "Tailoring", "Crafting", "Art", "Research", "Hauling", "Cleaning", "Childcare", "Fishing", "HaulingUrgent" };
            foreach (var work in WorkAccess.Works())
            {
                var rule = state.Rule(work);
                rule.Enabled = supported.Contains(work.defName);
                if (!rule.Enabled) continue; // Keep hunting, dark study and unfamiliar mod work under manual control.
                bool emergency = WorkAccess.Emergency(work), utility = work.defName is "BasicWorker" or "HaulingUrgent";
                rule.MinSkill = 0; rule.FavorPassion = true; rule.AdultsOnly = WorkAccess.Dangerous(work);
                rule.Mode = emergency || utility || work.defName == "Hauling" ? "Everyone" : work.defName == "Childcare" && !children ? "Off" : "Count";
                rule.Amount = 1;
                rule.Priority = emergency || work.defName is "Doctor" or "HaulingUrgent" ? 1 : work.defName == "Hauling" ? 4 : work.defName is "Cleaning" or "BasicWorker" ? 3 : 2;
                rule.Backups = emergency || utility || work.defName is "Hauling" or "Cleaning" ? 0 : 1;
                rule.BackupPriority = 3;
                rule.FallbackPriority = emergency || utility ? 0 : 4;
                rule.Essential = work.defName is "Doctor" or "Cooking" or "Growing" or "Construction";
            }
        }
        public static string Export(PriorityMap state)
        {
            try
            {
                var xml = new XElement("mulldPriorities", new XAttribute("version", 2), new XAttribute("balanced", state.BalancedPolicy), state.Rules.Where(r => r.Work != null).Select(r => new XElement("rule", new XAttribute("work", r.Work.defName), new XAttribute("enabled", r.Enabled), new XAttribute("mode", r.Mode), new XAttribute("amount", r.Amount), new XAttribute("priority", r.Priority), new XAttribute("minSkill", r.MinSkill), new XAttribute("passion", r.FavorPassion), new XAttribute("adults", r.AdultsOnly), new XAttribute("backups", r.Backups), new XAttribute("backupPriority", r.BackupPriority), new XAttribute("fallback", r.FallbackPriority), new XAttribute("essential", r.Essential))));
                xml.Save(FilePath); return "Exported staffing rules to " + FilePath;
            }
            catch (Exception e) { Log.Warning("[mulld Priorities] Export: " + e.Message); return "Could not export rules: " + e.Message; }
        }
        public static string Import(PriorityMap state)
        {
            try
            {
                var xml = XElement.Load(FilePath);
                if (xml.Name != "mulldPriorities" || (int?)xml.Attribute("version") is not (1 or 2)) return "Unsupported preset format.";
                var rules = xml.Elements("rule").Select(e => new WorkRule { Work = DefDatabase<WorkTypeDef>.GetNamedSilentFail((string)e.Attribute("work")), Enabled = (bool)e.Attribute("enabled"), Mode = (string)e.Attribute("mode"), Amount = (int)e.Attribute("amount"), Priority = (int)e.Attribute("priority"), MinSkill = (int)e.Attribute("minSkill"), FavorPassion = (bool)e.Attribute("passion"), AdultsOnly = (bool)e.Attribute("adults"), Backups = (int?)e.Attribute("backups") ?? 0, BackupPriority = (int?)e.Attribute("backupPriority") ?? 3, FallbackPriority = (int?)e.Attribute("fallback") ?? 0, Essential = (bool?)e.Attribute("essential") ?? false }).ToList();
                if (rules.Any(r => r.Priority < 1 || r.Priority > 4 || r.Amount < 0 || r.MinSkill < 0 || r.MinSkill > 20 || r.Backups < 0 || r.BackupPriority < 3 || r.BackupPriority > 4 || r.FallbackPriority < 0 || r.FallbackPriority > 4 || !new[] { "Count", "Percent", "Everyone", "Off" }.Contains(r.Mode) || r.Mode == "Percent" && r.Amount > 100)) return "Preset contains invalid rule values; nothing imported.";
                state.Rules = rules.Where(r => r.Work != null).GroupBy(r => r.Work).Select(g => g.First()).ToList();
                state.BalancedPolicy = (bool?)xml.Attribute("balanced") ?? false;
                return "Imported " + state.Rules.Count + " rules. Assignments unchanged; stage rules to review.";
            }
            catch (Exception e) { Log.Warning("[mulld Priorities] Import: " + e.Message); return "Could not import rules: " + e.Message; }
        }
    }
}
