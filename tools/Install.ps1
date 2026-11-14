param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$modsRoot = [IO.Path]::GetFullPath((Join-Path $GamePath 'Mods'))
$destination = [IO.Path]::GetFullPath((Join-Path $modsRoot 'mulldPriorities'))
if (!(Test-Path -LiteralPath (Join-Path $GamePath 'Assembly-CSharp.dll'))) { throw 'GamePath is not a RimWorld installation.' }
if (!$destination.StartsWith($modsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Destination must be inside the game Mods folder.' }
if (!(Test-Path -LiteralPath (Join-Path $repo 'Assemblies/Mulld.Priorities.dll'))) { throw 'Build the assembly first.' }
$legacy = [IO.Path]::GetFullPath((Join-Path $modsRoot 'muldPriorities'))
if (!$legacy.StartsWith($modsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Legacy destination must be inside Mods.' }
if (Test-Path -LiteralPath $legacy) {
    $legacyMetadata = [xml](Get-Content -LiteralPath (Join-Path $legacy 'About/About.xml') -Raw)
    if ($legacyMetadata.ModMetaData.packageId -ne 'mulld.priorities') { throw 'Refusing to rename a different legacy mod.' }
    if (Test-Path -LiteralPath $destination) { throw 'Both legacy and canonical mod folders exist; refusing a duplicate installation.' }
    Move-Item -LiteralPath $legacy -Destination $destination
}
$existing = Join-Path $destination 'About/About.xml'
if (Test-Path -LiteralPath $existing) {
    $metadata = [xml](Get-Content -LiteralPath $existing -Raw)
    if ($metadata.ModMetaData.packageId -ne 'mulld.priorities') { throw 'Refusing to overwrite a different mod.' }
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($folder in @('About', 'Defs', 'Patches', 'Textures')) { Copy-Item -LiteralPath (Join-Path $repo $folder) -Destination $destination -Recurse -Force }
$assemblyFolder = Join-Path $destination 'Assemblies'
New-Item -ItemType Directory -Path $assemblyFolder -Force | Out-Null
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo 'Assemblies') -File) {
    $target = Join-Path $assemblyFolder $file.Name
    $pending = $target + '.pending'
    Copy-Item -LiteralPath $file.FullName -Destination $pending -Force
    # Replacing the directory entry avoids writing into an assembly mapped by
    # an existing game process. That process keeps its loaded version until restart.
    if (Test-Path -LiteralPath $target) { [IO.File]::Replace($pending, $target, [System.Management.Automation.Language.NullString]::Value) }
    else { Move-Item -LiteralPath $pending -Destination $target }
}
Write-Output "Installed mulld Priorities to $destination"
