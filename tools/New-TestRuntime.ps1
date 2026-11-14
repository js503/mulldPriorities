param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $repo 'artifacts/test-runtime'
if (!(Test-Path -LiteralPath (Join-Path $GamePath 'Assembly-CSharp.dll'))) { throw 'Not a RimWorld installation.' }
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
# Keep all writable engine files separate from the running game. Large official
# data directories are read through junctions; no game binaries are redistributed.
Get-ChildItem -LiteralPath $GamePath -File | Where-Object Extension -NotIn @('.log','.cfg','.ini') | ForEach-Object {
    $target = Join-Path $runtime $_.Name
    if (!(Test-Path -LiteralPath $target)) { Copy-Item -LiteralPath $_.FullName -Destination $target }
}
Get-ChildItem -LiteralPath $GamePath -Directory | Where-Object Name -ne 'Mods' | ForEach-Object {
    $target = Join-Path $runtime $_.Name
    if (!(Test-Path -LiteralPath $target)) { New-Item -ItemType Junction -Path $target -Target $_.FullName | Out-Null }
}
$mods = Join-Path $runtime 'Mods'; New-Item -ItemType Directory -Path $mods -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $GamePath 'Mods') -Directory | Where-Object Name -NotIn @('mulldPriorities','muldPriorities') | ForEach-Object {
    $target = Join-Path $mods $_.Name
    if (!(Test-Path -LiteralPath $target)) { New-Item -ItemType Junction -Path $target -Target $_.FullName | Out-Null }
}
& (Join-Path $PSScriptRoot 'Install.ps1') -GamePath $runtime
Write-Output "Test runtime: $runtime"
