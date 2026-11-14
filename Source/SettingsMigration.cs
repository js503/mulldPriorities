using System.IO;

namespace Mulld.Priorities
{
    public static class SettingsMigration
    {
        public static bool Migrate(string configFolder)
        {
            string legacy = Path.Combine(configFolder, "Mod_muldPriorities_MulldMod.xml");
            string canonical = Path.Combine(configFolder, "Mod_mulldPriorities_MulldMod.xml");
            if (!File.Exists(legacy) || File.Exists(canonical)) return false;
            try { File.Copy(legacy, canonical, false); return true; }
            catch (IOException) { if (File.Exists(canonical)) return false; throw; }
        }
    }
}
