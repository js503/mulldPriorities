$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo 'Textures/UI/Mulld'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Add-Type -AssemblyName System.Drawing
$bitmap = [Drawing.Bitmap]::new(32,32)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$pen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(245,245,245,240),2.2)
$path = [Drawing.Drawing2D.GraphicsPath]::new()
try {
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::Transparent)
    $path.AddBezier(3,16,11.67,6.67,20.33,6.67,29,16)
    $path.AddBezier(29,16,20.33,25.33,11.67,25.33,3,16)
    $path.CloseFigure()
    $graphics.DrawPath($pen,$path)
    $graphics.DrawEllipse($pen,11.5,11.5,9,9)
    $bitmap.Save((Join-Path $output 'Preview.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally { $path.Dispose();$pen.Dispose();$graphics.Dispose();$bitmap.Dispose() }
Write-Output 'Built cached 32px preview icon.'
