param([string]$BrowserPath = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe', [switch]$FeatureImagesOnly)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $repo 'artifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
if (!(Test-Path -LiteralPath $BrowserPath)) { throw 'Supply a Chromium browser with -BrowserPath.' }
if (!$FeatureImagesOnly) {
$preview = Join-Path $repo 'About/Preview.png'
$before = if (Test-Path $preview) { (Get-Item $preview).LastWriteTimeUtc } else { [datetime]::MinValue }
$profile = Join-Path $artifacts ('cover-render-' + [guid]::NewGuid().ToString('N'))
$url = ([uri](Join-Path $repo 'docs/workshop/cover.html')).AbsoluteUri
$arguments = @('--headless','--disable-gpu','--no-first-run','--hide-scrollbars','--force-device-scale-factor=1','--window-size=1280,720',('--screenshot="' + $preview + '"'),('--user-data-dir="' + $profile + '"'),('"' + $url + '"'))
$process = Start-Process -FilePath $BrowserPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru -RedirectStandardError (Join-Path $artifacts 'cover-render.log')
if ($process.ExitCode -ne 0 -or !(Test-Path $preview) -or (Get-Item $preview).LastWriteTimeUtc -le $before) { throw 'Cover export failed; see artifacts/cover-render.log.' }
Add-Type -AssemblyName System.Drawing
$image = [Drawing.Image]::FromFile($preview)
try { if ($image.Width -ne 1280 -or $image.Height -ne 720) { throw 'Expected a 1280x720 cover.' } } finally { $image.Dispose() }
if ((Get-Item $preview).Length -ge 1MB) { throw 'Workshop preview exceeds 1 MB.' }
}
Add-Type -AssemblyName System.Drawing
$screen = [Drawing.Image]::FromFile((Join-Path $repo 'docs/screenshots/work-preview.png'))
try {
    foreach ($crop in @(@{Name='needs';Y=39;H=101},@{Name='rules';Y=180;H=270},@{Name='preview';Y=478;H=330})) {
        $bitmap = [Drawing.Bitmap]::new(1280,$crop.H)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.DrawImage($screen,[Drawing.Rectangle]::new(0,0,1280,$crop.H),0,$crop.Y,1280,$crop.H,[Drawing.GraphicsUnit]::Pixel)
            $bitmap.Save((Join-Path $repo ('docs/workshop/images/'+$crop.Name+'.png')),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $screen.Dispose() }
Write-Output 'Built current real-game feature crops; Cinema cover retained when FeatureImagesOnly is set.'
