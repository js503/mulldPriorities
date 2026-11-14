using System;
using System.IO;
using Mulld.Priorities;

public static class SettingsMigrationTests
{
    public static string Run(string artifacts)
    {
        string folder=Path.Combine(artifacts,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string legacy=Path.Combine(folder,"Mod_muldPriorities_MulldMod.xml"),canonical=Path.Combine(folder,"Mod_mulldPriorities_MulldMod.xml");
        if(SettingsMigration.Migrate(folder)||File.Exists(canonical))throw new Exception("Missing legacy settings must be a no-op");
        File.WriteAllText(legacy,"<SettingsBlock><ModSettings><performanceDiagnostics>true</performanceDiagnostics></ModSettings></SettingsBlock>");
        if(!SettingsMigration.Migrate(folder)||File.ReadAllText(legacy)!=File.ReadAllText(canonical))throw new Exception("Migration must preserve original bytes and file");
        File.WriteAllText(legacy,"older settings");
        if(SettingsMigration.Migrate(folder)||File.ReadAllText(canonical)=="older settings")throw new Exception("Existing canonical settings must win");
        return "3 settings migration checks passed.";
    }
}
