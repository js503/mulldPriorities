param([string]$Owner = 'js503', [string]$Repository = 'mulldPriorities')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if ($Owner -notmatch '^[A-Za-z0-9-]+$' -or $Repository -notmatch '^[A-Za-z0-9_.-]+$') { throw 'Invalid GitHub owner or repository name.' }
if (!(Get-Command gh -ErrorAction SilentlyContinue)) { throw 'Install the official GitHub CLI and run gh auth login, then rerun this script.' }
Push-Location $repo
try {
    $login = gh api user --jq .login
    if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI is not authenticated. Run gh auth login first.' }
    if ($login -ne $Owner) { throw "Authenticated as $login; expected $Owner. No repository changed." }
    if (git status --porcelain) { throw 'Commit or stash local changes before publishing.' }
    $fullName = "$Owner/$Repository"
    $expectedRemote = "https://github.com/$fullName.git"
    $remote = git remote get-url origin 2>$null
    if ($LASTEXITCODE -eq 0 -and $remote -ne $expectedRemote) { throw "origin points to another repository: $remote" }
    if (!$remote) { git remote add origin $expectedRemote; if ($LASTEXITCODE -ne 0) { throw 'Could not configure origin.' } }
    $visibility = gh repo view $fullName --json visibility --jq .visibility 2>$null
    if ($LASTEXITCODE -ne 0) {
        gh repo create $fullName --private --description 'mulld Priorities: colony needs and reviewable work assignments for RimWorld 1.7.'
        if ($LASTEXITCODE -ne 0) { throw 'Could not create the private GitHub repository.' }
    } elseif ($visibility -ne 'PRIVATE') { throw 'Expected a private repository. Refusing to publish to a public destination automatically.' }
    # Use the CLI credential helper for this invocation only; do not change
    # global Git authentication or store tokens in the repository/remote URL.
    git -c 'credential.https://github.com.helper=' -c 'credential.https://github.com.helper=!gh auth git-credential' push -u origin HEAD:main
    if ($LASTEXITCODE -ne 0) { throw 'Git push failed; no force push was attempted.' }
    Write-Output "Published https://github.com/$fullName on main."
} finally { Pop-Location }
