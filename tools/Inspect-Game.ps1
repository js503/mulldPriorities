param([string[]]$Types = @('Verse.WorkTypeDef'), [string]$Match = '.', [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
[System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving([System.Func[System.Runtime.Loader.AssemblyLoadContext,System.Reflection.AssemblyName,System.Reflection.Assembly]] ({
    param($context, $name)
    $candidate = Join-Path $GamePath ($name.Name + '.dll')
    if (Test-Path -LiteralPath $candidate) { return $context.LoadFromAssemblyPath($candidate) }
    return $null
}.GetNewClosure()))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $GamePath 'Assembly-CSharp.dll'))
foreach ($name in $Types) {
    $type = $assembly.GetType($name)
    if (!$type) { Write-Output "MISSING $name"; continue }
    Write-Output "TYPE $name"
    $type.GetMembers([Reflection.BindingFlags]'Public,Instance,Static,DeclaredOnly') |
        Where-Object { $_.ToString() -match $Match } |
        ForEach-Object { $_.ToString() }
}
