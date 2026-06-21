Add-Type -AssemblyName System.Drawing

function New-NavPlanBitmap([int]$size) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    [float]$s  = $size / 256.0
    [float]$cx = $size / 2.0
    [float]$cy = $size / 2.0

    # ── Background gradient circle ──────────────────────────────────────────
    [int]$pad = [Math]::Max(2, [int](4.0 * $s))
    $bgPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $bgPath.AddEllipse($pad, $pad, ($size - 2*$pad), ($size - 2*$pad))
    $pgb = [System.Drawing.Drawing2D.PathGradientBrush]::new($bgPath)
    $pgb.CenterPoint    = [System.Drawing.PointF]::new([float]$cx, [float]($cy - 20.0*$s))
    $pgb.CenterColor    = [System.Drawing.Color]::FromArgb(255, 28, 68, 128)
    $pgb.SurroundColors = [System.Drawing.Color[]]@([System.Drawing.Color]::FromArgb(255, 5, 12, 28))
    $g.FillPath($pgb, $bgPath)
    $pgb.Dispose(); $bgPath.Dispose()

    # ── Gold outer ring ─────────────────────────────────────────────────────
    [float]$ringW = [Math]::Max(1.0, 3.5 * $s)
    $ringPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 200, 152, 38), $ringW)
    [float]$rp = [Math]::Max(2.0, 5.0 * $s)
    $g.DrawEllipse($ringPen, $rp, $rp, [float]($size - 2.0*$rp), [float]($size - 2.0*$rp))
    $ringPen.Dispose()

    # ── Inner thin ring ─────────────────────────────────────────────────────
    if ($size -ge 48) {
        $innerPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(80, 200, 152, 38), [float](1.0 * $s))
        [float]$ip = 18.0 * $s
        $g.DrawEllipse($innerPen, $ip, $ip, [float]($size - 2.0*$ip), [float]($size - 2.0*$ip))
        $innerPen.Dispose()
    }

    $whiteBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(248, 252, 255, 255))
    $goldBrush  = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 205, 155, 35))

    # Scriptblock closure sees $g, $cx, $cy, $s from enclosing scope
    $drawPoint = {
        param($brush, [float]$tip, [float]$hw, [float]$tail, [float]$deg)
        [float]$rad = [float]($deg * [Math]::PI / 180.0)
        [float]$sa  = [float][Math]::Sin($rad)
        [float]$ca  = [float][Math]::Cos($rad)
        $pts = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new([float]($cx + $tip  * $sa), [float]($cy - $tip  * $ca)),
            [System.Drawing.PointF]::new([float]($cx + $hw   * $ca), [float]($cy + $hw   * $sa)),
            [System.Drawing.PointF]::new([float]($cx - $tail * $sa), [float]($cy + $tail * $ca)),
            [System.Drawing.PointF]::new([float]($cx - $hw   * $ca), [float]($cy - $hw   * $sa))
        )
        $g.FillPolygon($brush, $pts)
    }

    # ── Compass rose — 4 main points (N/S/E/W), white ───────────────────────
    foreach ($d in @(0.0, 90.0, 180.0, 270.0)) {
        & $drawPoint $whiteBrush ([float](72.0*$s)) ([float](9.0*$s)) ([float](9.0*$s)) ([float]$d)
    }

    # ── Compass rose — 4 diagonal points (NE/SE/SW/NW), gold ────────────────
    if ($size -ge 24) {
        foreach ($d in @(45.0, 135.0, 225.0, 315.0)) {
            & $drawPoint $goldBrush ([float](48.0*$s)) ([float](6.0*$s)) ([float](6.0*$s)) ([float]$d)
        }
    }

    # ── Gold north-hat triangle ──────────────────────────────────────────────
    if ($size -ge 32) {
        $hat = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new([float]$cx,              [float]($cy - 87.0*$s)),
            [System.Drawing.PointF]::new([float]($cx + 8.5*$s),  [float]($cy - 74.0*$s)),
            [System.Drawing.PointF]::new([float]($cx - 8.5*$s),  [float]($cy - 74.0*$s))
        )
        $g.FillPolygon($goldBrush, $hat)
    }

    # ── Airplane silhouette (white, top-down, nose pointing North) ────────────
    $planeBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    [float]$a = $s

    $fuse = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([float]$cx,           [float]($cy - 37.0*$a))
        [System.Drawing.PointF]::new([float]($cx + 6.0*$a),[float]($cy - 21.0*$a))
        [System.Drawing.PointF]::new([float]($cx + 7.0*$a),[float]($cy +  9.0*$a))
        [System.Drawing.PointF]::new([float]($cx + 4.0*$a),[float]($cy + 30.0*$a))
        [System.Drawing.PointF]::new([float]$cx,           [float]($cy + 37.0*$a))
        [System.Drawing.PointF]::new([float]($cx - 4.0*$a),[float]($cy + 30.0*$a))
        [System.Drawing.PointF]::new([float]($cx - 7.0*$a),[float]($cy +  9.0*$a))
        [System.Drawing.PointF]::new([float]($cx - 6.0*$a),[float]($cy - 21.0*$a))
    )
    $g.FillPolygon($planeBrush, $fuse)

    $lw = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([float]($cx -  7.0*$a),[float]($cy -  6.0*$a))
        [System.Drawing.PointF]::new([float]($cx - 51.0*$a),[float]($cy +  4.0*$a))
        [System.Drawing.PointF]::new([float]($cx - 48.0*$a),[float]($cy + 14.0*$a))
        [System.Drawing.PointF]::new([float]($cx -  7.0*$a),[float]($cy + 13.0*$a))
    )
    $g.FillPolygon($planeBrush, $lw)

    $rw = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([float]($cx +  7.0*$a),[float]($cy -  6.0*$a))
        [System.Drawing.PointF]::new([float]($cx + 51.0*$a),[float]($cy +  4.0*$a))
        [System.Drawing.PointF]::new([float]($cx + 48.0*$a),[float]($cy + 14.0*$a))
        [System.Drawing.PointF]::new([float]($cx +  7.0*$a),[float]($cy + 13.0*$a))
    )
    $g.FillPolygon($planeBrush, $rw)

    $ls = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([float]($cx -  4.0*$a),[float]($cy + 22.0*$a))
        [System.Drawing.PointF]::new([float]($cx - 23.0*$a),[float]($cy + 30.0*$a))
        [System.Drawing.PointF]::new([float]($cx - 20.0*$a),[float]($cy + 38.0*$a))
        [System.Drawing.PointF]::new([float]($cx -  3.0*$a),[float]($cy + 32.0*$a))
    )
    $g.FillPolygon($planeBrush, $ls)

    $rs = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([float]($cx +  4.0*$a),[float]($cy + 22.0*$a))
        [System.Drawing.PointF]::new([float]($cx + 23.0*$a),[float]($cy + 30.0*$a))
        [System.Drawing.PointF]::new([float]($cx + 20.0*$a),[float]($cy + 38.0*$a))
        [System.Drawing.PointF]::new([float]($cx +  3.0*$a),[float]($cy + 32.0*$a))
    )
    $g.FillPolygon($planeBrush, $rs)

    # ── Gold center dot ─────────────────────────────────────────────────────
    if ($size -ge 32) {
        [float]$dr = 4.5 * $s
        $g.FillEllipse($goldBrush, [float]($cx - $dr), [float]($cy - $dr), [float]($dr * 2.0), [float]($dr * 2.0))
    }

    $g.Dispose(); $whiteBrush.Dispose(); $goldBrush.Dispose(); $planeBrush.Dispose()
    return $bmp
}

function Save-MultiIco([int[]]$sizes, [string]$path) {
    $bmps    = $sizes | ForEach-Object { New-NavPlanBitmap $_ }
    $streams = $bmps  | ForEach-Object {
        $ms = [System.IO.MemoryStream]::new()
        $_.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $ms
    }

    $fs = [System.IO.File]::Open($path, [System.IO.FileMode]::Create)
    $bw = [System.IO.BinaryWriter]::new($fs)

    $n   = $sizes.Count
    $off = [uint32](6 + $n * 16)

    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$n)

    for ($i = 0; $i -lt $n; $i++) {
        $w = $sizes[$i]
        $bw.Write([byte]($w -ge 256 ? 0 : $w))
        $bw.Write([byte]($w -ge 256 ? 0 : $w))
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$streams[$i].Length)
        $bw.Write([uint32]$off)
        $off += [uint32]$streams[$i].Length
    }
    $streams | ForEach-Object { $bw.Write($_.ToArray()) }

    $bw.Close(); $fs.Close()
    $bmps    | ForEach-Object { $_.Dispose() }
    $streams | ForEach-Object { $_.Dispose() }
}

$ico = "C:\github\NavigationPlan\NavigationPlan\NavigationPlan.ico"
$png = "C:\github\NavigationPlan\NavigationPlan\NavigationPlan_preview.png"

Save-MultiIco -sizes @(16, 24, 32, 48, 256) -path $ico

$preview = New-NavPlanBitmap 256
$preview.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()

Write-Host "Icon  : $ico"
Write-Host "Preview: $png"
