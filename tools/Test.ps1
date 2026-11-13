$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $repo 'Source/Planner.cs'), (Join-Path $repo 'Tests/PlannerTests.cs'), (Join-Path $repo 'Tests/ReferencePlanner.cs'), (Join-Path $repo 'Source/PerformanceMetrics.cs'), (Join-Path $repo 'Tests/PerformanceTests.cs')
[PlannerTests]::Run()
[PerformanceTests]::Run()
foreach ($file in @('About/About.xml', 'Defs/MainButtonDefs.xml', 'Patches/WorkTab.xml')) {
    $xml = [xml](Get-Content -LiteralPath (Join-Path $repo $file) -Raw)
    if (!$xml.DocumentElement) { throw "Invalid XML: $file" }
}
Write-Output 'Mod metadata and definition XML parsed.'
