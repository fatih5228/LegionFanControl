Add-Type -AssemblyName System.Drawing

function New-FanBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::FromArgb(255, 0x12, 0x12, 0x12))

    $red = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0xE2, 0x23, 0x1A))
    $cx = $size / 2.0; $cy = $size / 2.0
    $r = $size * 0.46   # dis yari cap
    $g.TranslateTransform($cx, $cy)

    # 4 kanat: her biri merkezden disa acilan elips-benzeri sekil
    for ($i = 0; $i -lt 4; $i++) {
        $g.ResetTransform()
        $g.TranslateTransform($cx, $cy)
        $g.RotateTransform($i * 90)
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        # kanat: merkez yakinindan baslayip saga-donerek disa giden egriler
        $bw = $r * 0.55  # kanat genisligi
        $path.AddBezier(0, 0,  $r*0.75, -$bw*0.15,  $r*0.95, -$bw*0.9,  $r*0.30, -$bw*1.15)
        $path.AddBezier($r*0.30, -$bw*1.15,  -$r*0.10, -$bw*0.85,  -$r*0.05, -$bw*0.30,  0, 0)
        $g.FillPath($red, $path)
    }

    # merkez gobek
    $g.ResetTransform()
    $hub = $size * 0.30
    $dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0x12, 0x12, 0x12))
    $g.FillEllipse($red, $cx - $hub/2 - 1, $cy - $hub/2 - 1, $hub + 2, $hub + 2)
    $g.FillEllipse($dark, $cx - $hub*0.35, $cy - $hub*0.35, $hub*0.7, $hub*0.7)

    $g.Dispose(); $red.Dispose(); $dark.Dispose()
    return $bmp
}

$sizes = 16, 32, 48
$pngBlobs = @()
foreach ($s in $sizes) {
    $bmp = New-FanBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBlobs += ,$ms.ToArray()
    $bmp.Dispose(); $ms.Dispose()
}

$out = 'C:\Users\FTHH\LegionY520FanControl\app.ico'
$fs = New-Object System.IO.FileStream $out, ([System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter $fs
# ICONDIR
$bw.Write([uint16]0)      # reserved
$bw.Write([uint16]1)      # type = icon
$bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bw.Write([byte]$s)   # width
    $bw.Write([byte]$s)   # height
    $bw.Write([byte]0)    # colors
    $bw.Write([byte]0)    # reserved
    $bw.Write([uint16]1)  # planes
    $bw.Write([uint16]32) # bitcount
    $bw.Write([uint32]$pngBlobs[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngBlobs[$i].Length
}
foreach ($blob in $pngBlobs) { $bw.Write($blob) }
$bw.Flush(); $fs.Close()
Write-Output ("ICO yazildi: " + (Get-Item $out).Length + " bayt")
