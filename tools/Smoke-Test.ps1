param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [switch]$CurrentModList,
    [string]$ProfileName = 'smoke-profile',
    [string]$SavePath,
    [switch]$Baseline
)
$ErrorActionPreference = 'Stop'
if ($Baseline -and !$CurrentModList) { throw 'Baseline requires CurrentModList.' }
$repo = Split-Path $PSScriptRoot -Parent
if ($ProfileName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'ProfileName must be a simple directory name.' }
$profile = Join-Path $repo ('artifacts/' + $ProfileName)
$config = Join-Path $profile 'Config'
New-Item -ItemType Directory -Path $config -Force | Out-Null
if ($CurrentModList) {
    $normalConfig = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Config'
    Get-ChildItem -LiteralPath $normalConfig -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $config -Force }
    $mods = [xml](Get-Content -LiteralPath (Join-Path $config 'ModsConfig.xml') -Raw)
    $testMod = if ($Baseline) { 'mulld.compatibilityprobe' } else { 'mulld.priorities' }
    if ($Baseline) { @($mods.SelectNodes('/ModsConfigData/activeMods/li') | Where-Object InnerText -eq 'mulld.priorities') | ForEach-Object { $_.ParentNode.RemoveChild($_) | Out-Null } }
    if (@($mods.ModsConfigData.activeMods.li) -notcontains $testMod) {
        $entry = $mods.CreateElement('li'); $entry.InnerText = $testMod
        $mods.ModsConfigData.activeMods.AppendChild($entry) | Out-Null
    }
    @($mods.ModsConfigData.activeMods.li) | Set-Content -LiteralPath (Join-Path $profile 'requested-mods.txt')
    Get-FileHash -LiteralPath (Join-Path $normalConfig 'ModsConfig.xml') | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $profile 'source-config-hash.json')
} else {
    $mods = [xml]'<ModsConfigData><version>1.7.5005</version><activeMods><li>brrainz.harmony</li><li>ludeon.rimworld</li><li>mulld.priorities</li></activeMods><knownExpansions /></ModsConfigData>'
}
$mods.Save((Join-Path $config 'ModsConfig.xml'))
$prefs = [xml]'<PrefsData><screenWidth>1280</screenWidth><screenHeight>900</screenHeight><fullscreen>False</fullscreen><runInBackground>True</runInBackground><devMode>True</devMode><volumeGame>0</volumeGame><volumeMusic>0</volumeMusic></PrefsData>'
$prefs.Save((Join-Path $config 'Prefs.xml'))
$arguments = @('-mulldsmoketest', ('"-savedatafolder=' + $profile + '"'), ('"-logfile=' + (Join-Path $profile 'Player.log') + '"'))
if ($Baseline) { $arguments += '-mulldbaseline' }
if ($SavePath) {
    if (!(Test-Path -LiteralPath $SavePath) -or (Get-Item -LiteralPath $SavePath).Length -eq 0) { throw 'SavePath must be a nonempty save file.' }
    $saves = Join-Path $profile 'Saves'; New-Item -ItemType Directory -Path $saves -Force | Out-Null
    Copy-Item -LiteralPath $SavePath -Destination (Join-Path $saves 'mulld-compat-copy.rws') -Force
    Get-FileHash -LiteralPath $SavePath | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $profile 'source-save-hash.json')
    $arguments += '-mulldloadtest'
} else { $arguments += '-quicktest' }
$process = Start-Process -FilePath (Join-Path $GamePath 'RimWorld.exe') -ArgumentList $arguments -WorkingDirectory $GamePath -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $profile 'stdout.log') -RedirectStandardError (Join-Path $profile 'stderr.log')
Write-Output "Smoke test PID: $($process.Id)"
Write-Output "Isolated results: $profile"
if ($process.WaitForExit(1000)) { Write-Output "Launcher exited: $($process.ExitCode)" }
