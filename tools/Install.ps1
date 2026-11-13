param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$modsRoot = [IO.Path]::GetFullPath((Join-Path $GamePath 'Mods'))
$destination = [IO.Path]::GetFullPath((Join-Path $modsRoot 'muldPriorities'))
if (!(Test-Path -LiteralPath (Join-Path $GamePath 'Assembly-CSharp.dll'))) { throw 'GamePath is not a RimWorld installation.' }
if (!$destination.StartsWith($modsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Destination must be inside the game Mods folder.' }
if (!(Test-Path -LiteralPath (Join-Path $repo 'Assemblies/Mulld.Priorities.dll'))) { throw 'Build the assembly first.' }
$existing = Join-Path $destination 'About/About.xml'
if (Test-Path -LiteralPath $existing) {
    $metadata = [xml](Get-Content -LiteralPath $existing -Raw)
    if ($metadata.ModMetaData.packageId -ne 'mulld.priorities') { throw 'Refusing to overwrite a different mod.' }
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($folder in @('About', 'Defs', 'Patches', 'Assemblies')) { Copy-Item -LiteralPath (Join-Path $repo $folder) -Destination $destination -Recurse -Force }
Write-Output "Installed mulld Priorities to $destination"
