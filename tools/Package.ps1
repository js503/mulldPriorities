$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $repo 'artifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$version = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $repo 'Assemblies/Mulld.Priorities.dll')).Version.ToString(3)
$destination = Join-Path $artifacts ('muldPriorities-' + $version + '.zip')
$paths = @('About', 'Defs', 'Patches', 'Textures', 'Assemblies', 'README.md', 'CHANGELOG.md') | ForEach-Object { Join-Path $repo $_ }
Compress-Archive -LiteralPath $paths -DestinationPath $destination -Force
Write-Output "Packaged $destination"
