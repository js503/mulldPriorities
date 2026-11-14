using System;
using System.Collections.Generic;
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
            foreach (var existing in state.Rules) { existing.Enabled = false; existing.IgnoreMainJobLimit = false; }
            var children = state.map.mapPawns.FreeColonistsSpawned.Any(p => !WorkAccess.Adult(p));
            var supported = new[] { "Firefighter", "Patient", "PatientBedRest", "BasicWorker", "Doctor", "Cooking", "Construction", "Growing", "Mining", "PlantCutting", "Handling", "Warden", "Smithing", "Tailoring", "Crafting", "Art", "Research", "Hauling", "Cleaning", "Childcare", "Fishing", "HaulingUrgent" };
            foreach (var work in WorkAccess.Works())
            {
                var rule = state.Rule(work);
                rule.Enabled = supported.Contains(work.defName);
                rule.IgnoreMainJobLimit = false;
                if (!rule.Enabled) continue;
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
            state.PresetName = "Balanced"; state.PresetDirty = false;
        }
        public static XElement Capture(PriorityMap state) => new XElement("mulldPriorities", new XAttribute("version", 3), new XAttribute("balanced", state.BalancedPolicy), new XAttribute("mainJobs", state.MaxMainJobs),
            state.Rules.Where(r => r.Work != null).Select(r => new XElement("rule", new XAttribute("work", r.Work.defName), new XAttribute("enabled", r.Enabled), new XAttribute("mode", r.Mode), new XAttribute("amount", r.Amount), new XAttribute("priority", r.Priority), new XAttribute("minSkill", r.MinSkill), new XAttribute("passion", r.FavorPassion), new XAttribute("adults", r.AdultsOnly), new XAttribute("backups", r.Backups), new XAttribute("backupPriority", r.BackupPriority), new XAttribute("fallback", r.FallbackPriority), new XAttribute("essential", r.Essential), new XAttribute("ignoreMainLimit", r.IgnoreMainJobLimit))));
        public static string Decode(XElement xml, out List<WorkRule> rules, out bool balanced, out int? mainJobs)
        {
            rules = null; balanced = false; mainJobs = null;
            try
            {
                int version = (int?)xml.Attribute("version") ?? 0;
                if (xml.Name != "mulldPriorities" || version is not (1 or 2 or 3)) return "Unsupported preset format.";
                var policy = xml.Attribute("balanced");
                if (policy != null && !bool.TryParse(policy.Value, out balanced)) return "Preset contains an invalid balanced policy; nothing imported.";
                mainJobs = (int?)xml.Attribute("mainJobs");
                if (mainJobs.HasValue && (mainJobs < 1 || mainJobs > 10)) return "Invalid primary-job limit.";
                var parsed = new List<WorkRule>(); var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var e in xml.Elements("rule"))
                {
                    string work = (string)e.Attribute("work");
                    if (string.IsNullOrWhiteSpace(work) || !names.Add(work)) return "Preset contains a missing or duplicate job name.";
                    parsed.Add(new WorkRule { Work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(work), Enabled = (bool)e.Attribute("enabled"), Mode = (string)e.Attribute("mode"), Amount = (int)e.Attribute("amount"), Priority = (int)e.Attribute("priority"), MinSkill = (int)e.Attribute("minSkill"), FavorPassion = (bool)e.Attribute("passion"), AdultsOnly = (bool)e.Attribute("adults"), Backups = (int?)e.Attribute("backups") ?? 0, BackupPriority = (int?)e.Attribute("backupPriority") ?? 3, FallbackPriority = (int?)e.Attribute("fallback") ?? 0, Essential = (bool?)e.Attribute("essential") ?? false, IgnoreMainJobLimit = version >= 3 && ((bool?)e.Attribute("ignoreMainLimit") ?? false) });
                }
                if (parsed.Any(r => r.Priority < 1 || r.Priority > 4 || r.Amount < 0 || r.MinSkill < 0 || r.MinSkill > 20 || r.Backups < 0 || r.BackupPriority < 3 || r.BackupPriority > 4 || r.FallbackPriority is not (0 or 3 or 4) || !new[] { "Count", "Percent", "Everyone", "Off" }.Contains(r.Mode) || r.Mode == "Percent" && r.Amount > 100)) return "Preset contains invalid rule values; nothing imported.";
                rules = parsed.Where(r => r.Work != null).ToList(); return null;
            }
            catch (Exception e) { return "Invalid preset: " + e.Message; }
        }
        public static string ApplyDefinition(PriorityMap state, XElement definition)
        {
            string error = Decode(definition, out var rules, out bool balanced, out int? mainJobs);
            if (error != null) return error;
            state.Rules = rules; state.BalancedPolicy = balanced;
            if (mainJobs.HasValue) state.MaxMainJobs = mainJobs.Value;
            state.Invalidate(); return null;
        }
        public static string Export(PriorityMap state)
        {
            try { Capture(state).Save(FilePath); return "Exported staffing rules to " + FilePath; }
            catch (Exception e) { return "Could not export rules: " + e.Message; }
        }
        public static string Import(PriorityMap state)
        {
            try
            {
                string error = ApplyDefinition(state, XElement.Load(FilePath));
                if (error != null) return error;
                state.PresetName = "Custom"; state.PresetDirty = true;
                return "Imported " + state.Rules.Count + " rules. Priorities unchanged; Preview Changes to review.";
            }
            catch (Exception e) { return "Could not import rules: " + e.Message; }
        }
    }
    public static class PresetCatalog
    {
        public static readonly string[] BuiltIns = { "Balanced", "Clean", "Harvest", "Haul" };
        private static NamedPresetStore store;
        internal static NamedPresetStore Store => store ??= new NamedPresetStore(Path.Combine(GenFilePaths.ConfigFolderPath, "mulldPriorities-presets.xml"), xml => Presets.Decode(xml, out _, out _, out _));
        public static IEnumerable<string> Names => BuiltIns.Concat(Store.Names).Distinct(StringComparer.OrdinalIgnoreCase);
        public static bool Exists(string name) => BuiltIns.Contains(name?.Trim(), StringComparer.OrdinalIgnoreCase) || Store.Contains(name);
        public static string Load(PriorityMap state, string name, bool factory = false)
        {
            if (!factory && Store.Error != null) return Store.Error;
            if (!factory && Store.TryGet(name, out var definition))
            {
                string error = Presets.ApplyDefinition(state, definition); if (error != null) return error;
                state.PresetName = Store.Names.First(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)); state.PresetDirty = false; return null;
            }
            string canonical = BuiltIns.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (canonical == null) return "That preset is no longer available.";
            Presets.Balanced(state);
            string focus = canonical == "Clean" ? "Cleaning" : canonical == "Harvest" ? "Growing" : canonical == "Haul" ? "Hauling" : null;
            if (focus != null)
            {
                var work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(focus);
                if (work != null)
                {
                    var rule = state.Rule(work); rule.Enabled = true; rule.Mode = "Everyone"; rule.Priority = 1; rule.Backups = 0; rule.IgnoreMainJobLimit = true;
                }
            }
            state.PresetName = canonical; state.PresetDirty = factory && Store.Contains(canonical); state.Invalidate(); return null;
        }
        public static string Save(PriorityMap state, string name, bool overwrite)
        {
            string error = NamedPresetStore.ValidateName(name); if (error != null) return error;
            if (Exists(name) && !overwrite) return "That preset already exists. Confirm overwrite to replace it.";
            string canonical = Names.FirstOrDefault(n => n.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)) ?? name.Trim();
            error = Store.Save(canonical, Presets.Capture(state), overwrite);
            if (error == null) { state.PresetName = canonical; state.PresetDirty = false; }
            return error;
        }
    }
}
