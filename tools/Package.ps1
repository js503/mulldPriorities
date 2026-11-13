$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $repo 'artifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$destination = Join-Path $artifacts 'muldPriorities-0.1.0.zip'
$paths = @('About', 'Defs', 'Assemblies', 'README.md', 'CHANGELOG.md') | ForEach-Object { Join-Path $repo $_ }
Compress-Archive -LiteralPath $paths -DestinationPath $destination -Force
Write-Output "Packaged $destination"
