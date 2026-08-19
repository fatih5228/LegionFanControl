Add-Type -AssemblyName System.Drawing

function Convert-PngToIco([string]$pngPath, [string]$icoPath) {
    $src = [System.Drawing.Image]::FromFile($pngPath)
    $sizes = 16, 32, 48
    $pngBlobs = @()
    foreach ($s in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap $s, $s
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = 'HighQualityBicubic'
        $g.SmoothingMode = 'AntiAlias'
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.DrawImage($src, 0, 0, $s, $s)
        $g.Dispose()
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBlobs += ,$ms.ToArray()
        $bmp.Dispose(); $ms.Dispose()
    }
    $src.Dispose()

    $fs = New-Object System.IO.FileStream $icoPath, ([System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $bw.Write([byte]$sizes[$i]); $bw.Write([byte]$sizes[$i])
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$pngBlobs[$i].Length)
        $bw.Write([uint32]$offset)
        $offset += $pngBlobs[$i].Length
    }
    foreach ($blob in $pngBlobs) { $bw.Write($blob) }
    $bw.Flush(); $fs.Close()
    Write-Output ("yazildi: " + $icoPath + " (" + (Get-Item $icoPath).Length + " bayt)")
}

$dir = 'C:\Users\FTHH\LegionY520FanControl'
Convert-PngToIco (Join-Path $dir 'icon-start.png') (Join-Path $dir 'app.ico')
Convert-PngToIco (Join-Path $dir 'icon-stop.png')  (Join-Path $dir 'app_off.ico')
