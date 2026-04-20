using namespace System.Drawing
using namespace System.IO

# Generates PNGs and assembles a multi-resolution ICO containing PNG images.
$sizes = @(16,32,48)
$text = 'CC SD'
$tmpDir = Join-Path (Get-Location) 'tools\favicon_tmp'
if(Test-Path $tmpDir){ Remove-Item -Recurse -Force $tmpDir }
New-Item -ItemType Directory -Path $tmpDir | Out-Null

foreach($s in $sizes){
    $bmp = New-Object Drawing.Bitmap $s,$s
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.Clear([Drawing.Color]::Black)
    $fontSize = [Math]::Max(6, [int]($s * 0.38))
    $font = New-Object Drawing.Font 'Segoe UI',$fontSize,[Drawing.FontStyle]::Bold
    $fmt = New-Object Drawing.StringFormat
    $fmt.Alignment = [Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [Drawing.StringAlignment]::Center
    $rect = New-Object Drawing.RectangleF 0,0,$s,$s
    $brush = [Drawing.Brushes]::White
    $g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.DrawString($text,$font,$brush,$rect,$fmt)
    $g.Dispose()
    $file = Join-Path $tmpDir "favicon-$s.png"
    $bmp.Save($file,[Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# Assemble ICO that embeds the PNG images (modern ICO supports PNG entries)
$pngFiles = Get-ChildItem -Path $tmpDir -Filter 'favicon-*.png' | Sort-Object Name
$outIco = Join-Path (Get-Location) 'favicon.ico'
$ms = New-Object IO.MemoryStream
$bw = New-Object IO.BinaryWriter($ms)

# ICONDIR header
$bw.Write([uint16]0)   # reserved
$bw.Write([uint16]1)   # image type (1 = icon)
$bw.Write([uint16]$pngFiles.Count) # number of images

$offset = 6 + 16 * $pngFiles.Count
foreach($f in $pngFiles){
    $b = [IO.File]::ReadAllBytes($f.FullName)
    $s = [int]([regex]::Match($f.Name,'favicon-(\d+)').Groups[1].Value)
    # width and height bytes: 0 means 256, otherwise value
    $bw.Write([byte](if($s -ge 256){0}else{$s}))
    $bw.Write([byte](if($s -ge 256){0}else{$s}))
    $bw.Write([byte]0) # color palette
    $bw.Write([byte]0) # reserved
    $bw.Write([uint16]1) # color planes
    $bw.Write([uint16]32) # bits per pixel
    $bw.Write([uint32]$b.Length)
    $bw.Write([uint32]$offset)
    $offset += $b.Length
}

foreach($f in $pngFiles){
    $b = [IO.File]::ReadAllBytes($f.FullName)
    $bw.Write($b)
}

$bw.Flush()
[IO.File]::WriteAllBytes($outIco,$ms.ToArray())
$bw.Dispose()
$ms.Dispose()

# Copy to docs/ for GitHub Pages
$docsPath = Join-Path (Get-Location) 'docs'
if(-not (Test-Path $docsPath)){ New-Item -ItemType Directory -Path $docsPath | Out-Null }
Copy-Item -Path $outIco -Destination (Join-Path $docsPath 'favicon.ico') -Force

# Clean up temporary PNGs
Remove-Item -Recurse -Force $tmpDir

Write-Output "Generated $outIco and docs/favicon.ico"
