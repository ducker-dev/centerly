param([Parameter(Mandatory)] [string] $OutPath)

Add-Type -AssemblyName System.Drawing
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256

function New-RoundedPath([System.Drawing.RectangleF] $r, [float] $radius) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $radius * 2
    $p.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $p.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $p.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $p.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

$pngs = foreach ($s in $sizes) {
    $bmp = [System.Drawing.Bitmap]::new($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'

    # Background: rounded square with blue gradient.
    $bg = [System.Drawing.RectangleF]::new(0, 0, $s, $s)
    $bgPath = New-RoundedPath $bg ($s * 0.22)
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($bg,
        [System.Drawing.Color]::FromArgb(255, 59, 130, 246), [System.Drawing.Color]::FromArgb(255, 29, 78, 216), 45.0)
    $g.FillPath($brush, $bgPath)

    # Screen outline.
    $stroke = [Math]::Max(1.0, $s * 0.07)
    $inset = $s * 0.17
    $screen = [System.Drawing.RectangleF]::new($inset, $inset, $s - 2 * $inset, $s - 2 * $inset)
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(150, 255, 255, 255), $stroke)
    $g.DrawPath($pen, (New-RoundedPath $screen ($s * 0.08)))

    # Centered window.
    $w = $s * 0.38; $h = $s * 0.30
    $win = [System.Drawing.RectangleF]::new(($s - $w) / 2, ($s - $h) / 2, $w, $h)
    $g.FillPath([System.Drawing.Brushes]::White, (New-RoundedPath $win ([Math]::Max(0.5, $s * 0.04))))

    $g.Dispose()
    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

# ICO container with PNG-compressed entries (supported since Vista).
$out = [System.IO.MemoryStream]::new()
$bw = [System.IO.BinaryWriter]::new($out)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $bw.Write([byte]($s % 256)); $bw.Write([byte]($s % 256))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($png in $pngs) { $bw.Write($png) }
$bw.Flush()
New-Item -ItemType Directory -Force (Split-Path $OutPath) | Out-Null
[System.IO.File]::WriteAllBytes($OutPath, $out.ToArray())
"wrote $OutPath ($($out.Length) bytes)"
