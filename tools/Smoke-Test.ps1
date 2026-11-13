param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$profile = Join-Path $repo 'artifacts/smoke-profile'
$config = Join-Path $profile 'Config'
New-Item -ItemType Directory -Path $config -Force | Out-Null
$mods = [xml]'<ModsConfigData><version>1.7.5005</version><activeMods><li>ludeon.rimworld</li><li>mulld.priorities</li></activeMods><knownExpansions /></ModsConfigData>'
$mods.Save((Join-Path $config 'ModsConfig.xml'))
$prefs = [xml]'<PrefsData><screenWidth>1280</screenWidth><screenHeight>900</screenHeight><fullscreen>False</fullscreen><runInBackground>True</runInBackground><devMode>True</devMode><volumeGame>0</volumeGame><volumeMusic>0</volumeMusic></PrefsData>'
$prefs.Save((Join-Path $config 'Prefs.xml'))
$arguments = @('-quicktest', '-mulldsmoketest', ('"-savedatafolder=' + $profile + '"'), ('"-logfile=' + (Join-Path $profile 'Player.log') + '"'))
$process = Start-Process -FilePath (Join-Path $GamePath 'RimWorld.exe') -ArgumentList $arguments -WorkingDirectory $GamePath -WindowStyle Hidden -PassThru
Write-Output "Smoke test PID: $($process.Id)"
Write-Output "Isolated results: $profile"
