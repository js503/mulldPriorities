param([string]$MockOutput)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repo = Split-Path $PSScriptRoot -Parent
$art = [Drawing.Image]::FromFile((Join-Path $repo 'docs/workshop/brand-square.png'))
$screen = [Drawing.Image]::FromFile((Join-Path $repo 'docs/screenshots/work-preview.png'))
function Brush($hex) { [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($hex)) }
$cream = Brush '#fff1d5'; $gold = Brush '#e8bb60'; $muted = Brush '#b9c4c8'
$large = [Drawing.Font]::new('Arial',94,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$medium = [Drawing.Font]::new('Arial',58,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$body = [Drawing.Font]::new('Arial',26,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
$small = [Drawing.Font]::new('Arial',18,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$center = [Drawing.StringFormat]::new(); $center.Alignment = 'Center'
function Text($text,$font,$brush,$x,$y) { $g.DrawString($text,$font,$brush,[single]$x,[single]$y) }
function Art($x,$y,$w,$h) { $g.DrawImage($art,[Drawing.Rectangle]::new($x,$y,$w,$h),18,214,476,280,[Drawing.GraphicsUnit]::Pixel) }
try {
 foreach($variant in 1..4) {
  $bmp=[Drawing.Bitmap]::new(1280,720); $g=[Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode='AntiAlias'; $g.TextRenderingHint='AntiAliasGridFit'; $g.InterpolationMode='HighQualityBicubic'
  $gradient=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(0,0,1280,720),[Drawing.ColorTranslator]::FromHtml('#11191e'),[Drawing.ColorTranslator]::FromHtml('#2a2a25'),30)
  $g.FillRectangle($gradient,0,0,1280,720);$gradient.Dispose()
  $pen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#c7a052'),3);$g.DrawRectangle($pen,19,19,1242,682);$pen.Dispose()
  switch($variant) {
   1 {
    Text 'mulld' $large $cream 65 150; Text 'Priorities' $medium $gold 70 255
    Text 'Colony needs. Useful work.' $body $muted 75 365; Text 'Your approval.' $body $muted 75 405
    Art 600 170 600 353
    Text 'NEEDS  /  RULES  /  PREVIEW  /  APPLY' $small $gold 75 594
   }
   2 {
    $g.DrawString('mulld Priorities',$medium,$cream,[Drawing.RectangleF]::new(40,58,1200,85),$center)
    $g.DrawString('LESS JUGGLING. MORE DOING.',$small,$gold,[Drawing.RectangleF]::new(40,151,1200,40),$center)
    Art 330 220 620 365
    $g.DrawString('Colony needs. Useful work. Your approval.',$body,$muted,[Drawing.RectangleF]::new(40,623,1200,50),$center)
   }
   3 {
    Text 'mulld' $large $cream 62 48; Text 'Priorities' $medium $gold 372 94
    Text 'SEE THE PLAN. THEN APPLY IT.' $small $muted 73 182
    $g.DrawImage($screen,[Drawing.Rectangle]::new(68,245,1144,317),0,455,1280,355,[Drawing.GraphicsUnit]::Pixel)
    Text 'IN YOUR WORK TAB  /  MANUAL APPROVAL BY DEFAULT' $small $gold 73 646
   }
   4 {
    $g.DrawString('mulld',$large,$cream,[Drawing.RectangleF]::new(40,73,1200,130),$center)
    $g.DrawString('Priorities',$medium,$gold,[Drawing.RectangleF]::new(40,187,1200,90),$center)
    $g.DrawString('A little order. A better colony.',$body,$muted,[Drawing.RectangleF]::new(40,297,1200,50),$center)
    $g.DrawImage($screen,[Drawing.Rectangle]::new(120,393,1040,167),0,605,1280,205,[Drawing.GraphicsUnit]::Pixel)
    $g.DrawString('NEEDS  /  RULES  /  PREVIEW  /  APPLY',$small,$gold,[Drawing.RectangleF]::new(40,645,1200,40),$center)
   }
  }
  if($variant -eq 1) { $bmp.Save((Join-Path $repo 'About/Preview.png'),[Drawing.Imaging.ImageFormat]::Png) }
  if($MockOutput) { New-Item -ItemType Directory -Path $MockOutput -Force | Out-Null; $bmp.Save((Join-Path $MockOutput "cover-$variant.png"),[Drawing.Imaging.ImageFormat]::Png) }
  $g.Dispose();$bmp.Dispose()
 }
 # Consistent real-game crops: full content width, no padded button blowups.
 $crops=@(@{Name='needs';Y=175;H=85},@{Name='rules';Y=265;H=196},@{Name='preview';Y=566;H=245})
 foreach($crop in $crops) {
  $bmp=[Drawing.Bitmap]::new(1280,$crop.H);$g=[Drawing.Graphics]::FromImage($bmp)
  $g.DrawImage($screen,[Drawing.Rectangle]::new(0,0,1280,$crop.H),0,$crop.Y,1280,$crop.H,[Drawing.GraphicsUnit]::Pixel)
  $bmp.Save((Join-Path $repo ('docs/workshop/images/'+$crop.Name+'.png')),[Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose();$bmp.Dispose()
 }
} finally { $art.Dispose();$screen.Dispose();$cream.Dispose();$gold.Dispose();$muted.Dispose();$large.Dispose();$medium.Dispose();$body.Dispose();$small.Dispose();$center.Dispose() }
if((Get-Item (Join-Path $repo 'About/Preview.png')).Length -ge 1MB){throw 'Workshop preview exceeds 1 MB.'}
Write-Output 'Built 1280x720 preview and full-width Colony Briefing crops.'

