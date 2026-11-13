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
            foreach (var work in WorkAccess.Works())
            {
                var rule = state.Rule(work);
                rule.Enabled = true; rule.MinSkill = 0; rule.FavorPassion = true; rule.AdultsOnly = WorkAccess.Dangerous(work);
                rule.Mode = WorkAccess.Emergency(work) || work.defName is "Hauling" or "BasicWorker" ? "Everyone" : "Count";
                rule.Amount = work.defName is "Doctor" or "Growing" or "Cleaning" ? 2 : 1;
                rule.Priority = WorkAccess.Emergency(work) || work.defName == "Doctor" ? 1 : work.defName == "Hauling" ? 4 : work.defName is "Cleaning" or "Research" or "BasicWorker" ? 3 : 2;
            }
        }
        public static string Export(PriorityMap state)
        {
            try
            {
                var xml = new XElement("mulldPriorities", new XAttribute("version", 1), state.Rules.Where(r => r.Work != null).Select(r => new XElement("rule", new XAttribute("work", r.Work.defName), new XAttribute("enabled", r.Enabled), new XAttribute("mode", r.Mode), new XAttribute("amount", r.Amount), new XAttribute("priority", r.Priority), new XAttribute("minSkill", r.MinSkill), new XAttribute("passion", r.FavorPassion), new XAttribute("adults", r.AdultsOnly))));
                xml.Save(FilePath); return "Exported staffing rules to " + FilePath;
            }
            catch (Exception e) { Log.Warning("[mulld Priorities] Export: " + e.Message); return "Could not export rules: " + e.Message; }
        }
        public static string Import(PriorityMap state)
        {
            try
            {
                var xml = XElement.Load(FilePath);
                if (xml.Name != "mulldPriorities" || (int?)xml.Attribute("version") != 1) return "Unsupported preset format.";
                var rules = xml.Elements("rule").Select(e => new WorkRule { Work = DefDatabase<WorkTypeDef>.GetNamedSilentFail((string)e.Attribute("work")), Enabled = (bool)e.Attribute("enabled"), Mode = (string)e.Attribute("mode"), Amount = (int)e.Attribute("amount"), Priority = (int)e.Attribute("priority"), MinSkill = (int)e.Attribute("minSkill"), FavorPassion = (bool)e.Attribute("passion"), AdultsOnly = (bool)e.Attribute("adults") }).ToList();
                if (rules.Any(r => r.Priority < 1 || r.Priority > 4 || r.Amount < 0 || r.MinSkill < 0 || r.MinSkill > 20 || !new[] { "Count", "Percent", "Everyone", "Off" }.Contains(r.Mode) || r.Mode == "Percent" && r.Amount > 100)) return "Preset contains invalid rule values; nothing imported.";
                state.Rules = rules.Where(r => r.Work != null).GroupBy(r => r.Work).Select(g => g.First()).ToList();
                return "Imported " + state.Rules.Count + " rules. Assignments unchanged; stage rules to review.";
            }
            catch (Exception e) { Log.Warning("[mulld Priorities] Import: " + e.Message); return "Could not import rules: " + e.Message; }
        }
    }
}
