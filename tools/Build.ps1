param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld', [switch]$WithSmokeTests, [switch]$ProbeOnly)
$ErrorActionPreference = 'Stop'
if ($ProbeOnly -and $WithSmokeTests) { throw 'ProbeOnly and WithSmokeTests are separate build modes.' }
$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo 'Assemblies'
if ($ProbeOnly) { $output = Join-Path $repo 'artifacts/compat-probe/Assemblies' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($name in @('Assembly-CSharp.dll', 'UnityStubs.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $GamePath $name))) { throw "Missing $name in $GamePath" }
}
# PowerShell 7.6 ships Roslyn and .NET 10 reference assemblies. This permits a
# reproducible offline build without installing an SDK or downloading packages.
if ([Environment]::Version.Major -lt 10) { throw 'Use PowerShell 7.6+ (.NET 10), or build Source/Mulld.Priorities.csproj with the .NET 10 SDK.' }
$references = @((Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll').FullName)
$references += Join-Path $GamePath 'Assembly-CSharp.dll'
$references += Join-Path $GamePath 'UnityStubs.dll'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $repo 'Source') -Filter '*.cs' -Recurse | Sort-Object FullName)
if ($ProbeOnly) { $sources = @(Get-Item -LiteralPath (Join-Path $repo 'Tests/CompatibilityProbe.cs')) }
if ($WithSmokeTests) {
    $sources += Get-Item -LiteralPath (Join-Path $repo 'Tests/SmokeHarness.cs')
}
$target = Join-Path $output 'Mulld.Priorities.dll'
$assemblyName = 'Mulld.Priorities'
if ($ProbeOnly) {
    $assemblyName = 'Mulld.CompatibilityProbe'; $target = Join-Path $output ($assemblyName + '.dll')
    $about = Join-Path $repo 'artifacts/compat-probe/About'; New-Item -ItemType Directory -Path $about -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $about 'About.xml'), '<ModMetaData><name>mulld compatibility test probe</name><author>mulld</author><packageId>mulld.compatibilityprobe</packageId><supportedVersions><li>1.7</li></supportedVersions><description>Isolated baseline logging only.</description></ModMetaData>')
}
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')) | Out-Null
$parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default
if ($WithSmokeTests) { $parseOptions = $parseOptions.WithPreprocessorSymbols([string[]]@('MULLD_SMOKE_TESTS')) }
$trees = [Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
foreach ($source in $sources) {
    $relative = [IO.Path]::GetRelativePath($repo, $source.FullName).Replace('\', '/')
    $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($source.FullName), $parseOptions, $relative, [Text.Encoding]::UTF8))
}
$metadata = [Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
foreach ($reference in $references) { $metadata.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($reference)) }
$options = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release).WithDeterministic($true)
$compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create($assemblyName, $trees, $metadata, $options)
$stream = [IO.File]::Create($target)
try { $result = $compilation.Emit($stream) } finally { $stream.Dispose() }
if (!$result.Success) { throw (($result.Diagnostics | ForEach-Object ToString) -join [Environment]::NewLine) }
Write-Output "Built $target"
