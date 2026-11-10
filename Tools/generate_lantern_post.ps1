param(
    [string]$outObj = "d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\GeneratedModels\Environment\23_Wooden_Lantern_Post.obj",
    [string]$outMtl = "d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\GeneratedModels\Environment\23_Wooden_Lantern_Post.mtl"
)

$outDir = Split-Path -Parent $outObj
if (!(Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$mtlContent = @"
# Material Library for Wooden Lantern Post
newmtl Mat_WoodPost
Ka 0.28 0.18 0.10
Kd 0.42 0.26 0.15
Ks 0.08 0.08 0.08
Ns 10
illum 2

newmtl Mat_WoodLight
Ka 0.35 0.25 0.15
Kd 0.58 0.40 0.24
Ks 0.10 0.10 0.10
Ns 12
illum 2

newmtl Mat_IronWrought
Ka 0.12 0.12 0.13
Kd 0.22 0.22 0.24
Ks 0.45 0.45 0.45
Ns 35
illum 2

newmtl Mat_LanternGlass
Ka 0.80 0.55 0.15
Kd 0.95 0.72 0.28
Ks 0.90 0.90 0.90
Ns 80
d 0.75
illum 2

newmtl Mat_LanternFlameCore
Ka 1.00 0.85 0.30
Kd 1.00 0.75 0.20
Ks 0.50 0.50 0.50
Ke 1.00 0.65 0.15
Ns 20
illum 2
"@
[System.IO.File]::WriteAllText($outMtl, $mtlContent, [System.Text.Encoding]::UTF8)

# Lists for OBJ
$verts = [System.Collections.Generic.List[string]]::new()
$normals = [System.Collections.Generic.List[string]]::new()
$faces = [System.Collections.Generic.List[string]]::new()
$currentMtl = ""

function Set-Material([string]$name) {
    $global:currentMtl = $name
    $faces.Add("usemtl $name")
}

function Add-Vertex([double]$x, [double]$y, [double]$z) {
    $verts.Add(("{0:F4} {1:F4} {2:F4}" -f $x, $y, $z))
    return $verts.Count
}

function Add-Quad([int]$v1, [int]$v2, [int]$v3, [int]$v4) {
    $faces.Add("f $v1 $v2 $v3 $v4")
}

function Add-Tri([int]$v1, [int]$v2, [int]$v3) {
    $faces.Add("f $v1 $v2 $v3")
}

function Add-Box([double]$cx, [double]$cy, [double]$cz, [double]$sx, [double]$sy, [double]$sz) {
    $hx = $sx * 0.5
    $hy = $sy * 0.5
    $hz = $sz * 0.5
    # 8 corners
    $v1 = Add-Vertex ($cx - $hx) ($cy - $hy) ($cz - $hz)
    $v2 = Add-Vertex ($cx + $hx) ($cy - $hy) ($cz - $hz)
    $v3 = Add-Vertex ($cx + $hx) ($cy + $hy) ($cz - $hz)
    $v4 = Add-Vertex ($cx - $hx) ($cy + $hy) ($cz - $hz)

    $v5 = Add-Vertex ($cx - $hx) ($cy - $hy) ($cz + $hz)
    $v6 = Add-Vertex ($cx + $hx) ($cy - $hy) ($cz + $hz)
    $v7 = Add-Vertex ($cx + $hx) ($cy + $hy) ($cz + $hz)
    $v8 = Add-Vertex ($cx - $hx) ($cy + $hy) ($cz + $hz)

    # South (Z-)
    Add-Quad $v1 $v4 $v3 $v2
    # North (Z+)
    Add-Quad $v5 $v6 $v7 $v8
    # West (X-)
    Add-Quad $v1 $v5 $v8 $v4
    # East (X+)
    Add-Quad $v2 $v3 $v7 $v6
    # Top (Y+)
    Add-Quad $v4 $v8 $v7 $v3
    # Bottom (Y-)
    Add-Quad $v1 $v2 $v6 $v5
}

function Add-Cylinder([double]$cx, [double]$yBottom, [double]$yTop, [double]$cz, [double]$rBottom, [double]$rTop, [int]$segments = 8) {
    $bVerts = @()
    $tVerts = @()
    for ($i = 0; $i -lt $segments; $i++) {
        $ang = (2.0 * [Math]::PI * $i) / $segments
        $cos = [Math]::Cos($ang)
        $sin = [Math]::Sin($ang)
        $bVerts += Add-Vertex ($cx + $rBottom * $cos) $yBottom ($cz + $rBottom * $sin)
        $tVerts += Add-Vertex ($cx + $rTop * $cos) $yTop ($cz + $rTop * $sin)
    }

    # Side quads
    for ($i = 0; $i -lt $segments; $i++) {
        $next = ($i + 1) % $segments
        Add-Quad $bVerts[$i] $tVerts[$i] $tVerts[$next] $bVerts[$next]
    }

    # Caps
    $bCenter = Add-Vertex $cx $yBottom $cz
    $tCenter = Add-Vertex $cx $yTop $cz
    for ($i = 0; $i -lt $segments; $i++) {
        $next = ($i + 1) % $segments
        Add-Tri $bCenter $bVerts[$next] $bVerts[$i]
        Add-Tri $tCenter $tVerts[$i] $tVerts[$next]
    }
}

# 1. Main Vertical Wood Pole
Set-Material "Mat_WoodPost"
Add-Cylinder 0.0 0.0 2.95 0.0 0.11 0.095 8
# Base support wedges
Add-Box 0.0 0.05 0.0 0.42 0.10 0.42
Add-Box 0.18 0.18 0.0 0.12 0.36 0.12
Add-Box -0.18 0.18 0.0 0.12 0.36 0.12
Add-Box 0.0 0.18 0.18 0.12 0.36 0.12
Add-Box 0.0 0.18 -0.18 0.12 0.36 0.12
# Carved top cap
Add-Box 0.0 3.00 0.0 0.18 0.10 0.18
Add-Cylinder 0.0 3.05 3.15 0.0 0.07 0.01 6

# 2. Horizontal Cantilever Arm
Set-Material "Mat_WoodLight"
# Horizontal beam projecting forward from z=-0.08 to z=0.92
Add-Box 0.0 2.82 0.42 0.12 0.14 1.00
# Tip beveled end
Add-Box 0.0 2.82 0.95 0.10 0.10 0.08

# 3. Diagonal 45-deg Brace Strut
# A series of overlapping boxes forming a 45 degree angle beam
for ($step = 0; $step -le 6; $step++) {
    $t = $step / 6.0
    $by = 2.35 + $t * 0.40
    $bz = 0.08 + $t * 0.42
    Add-Box 0.0 $by $bz 0.08 0.10 0.10
}

# 4. Iron Bands & Reinforcements
Set-Material "Mat_IronWrought"
# Pole iron bands
Add-Cylinder 0.0 0.45 0.52 0.0 0.125 0.125 8
Add-Cylinder 0.0 1.50 1.56 0.0 0.120 0.120 8
Add-Cylinder 0.0 2.70 2.88 0.0 0.125 0.125 8
# Arm brackets & bolts
Add-Box 0.0 2.82 0.00 0.15 0.17 0.16
Add-Box 0.0 2.82 0.85 0.14 0.16 0.08

# Hanging hook under the beam at z = 0.85
Add-Cylinder 0.0 2.74 2.68 0.85 0.02 0.02 6
# Chain links
Add-Cylinder 0.0 2.68 2.60 0.85 0.035 0.035 6
Add-Cylinder 0.0 2.60 2.52 0.85 0.035 0.035 6
Add-Cylinder 0.0 2.52 2.45 0.85 0.035 0.035 6

# 5. Lantern Housing (Centered at z = 0.85)
# Top suspension ring
Add-Cylinder 0.0 2.45 2.50 0.85 0.045 0.045 6
# Lantern Roof / Hood (flared pyramid cap)
Add-Cylinder 0.0 2.36 2.45 0.85 0.20 0.06 6
Add-Box 0.0 2.35 0.85 0.28 0.03 0.28
# Bottom tray of lantern
Add-Box 0.0 1.98 0.85 0.26 0.04 0.26
Add-Cylinder 0.0 1.90 1.98 0.85 0.02 0.07 6

# 4 Iron corner ribs holding glass cage
Add-Box 0.10 2.16 0.95 0.025 0.34 0.025
Add-Box -0.10 2.16 0.95 0.025 0.34 0.025
Add-Box 0.10 2.16 0.75 0.025 0.34 0.025
Add-Box -0.10 2.16 0.75 0.025 0.34 0.025

# 6. Glass Enclosure
Set-Material "Mat_LanternGlass"
Add-Box 0.0 2.16 0.85 0.19 0.32 0.19

# 7. Interior Glowing Flame / Candle Core
Set-Material "Mat_LanternFlameCore"
Add-Cylinder 0.0 2.08 2.22 0.85 0.04 0.02 6
Add-Box 0.0 2.18 0.85 0.05 0.08 0.05

# Build OBJ output string
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("# Wavefront OBJ: 23_Wooden_Lantern_Post")
[void]$sb.AppendLine("mtllib 23_Wooden_Lantern_Post.mtl")
[void]$sb.AppendLine("o 23_Wooden_Lantern_Post")
foreach ($v in $verts) {
    [void]$sb.AppendLine("v $v")
}
foreach ($f in $faces) {
    [void]$sb.AppendLine($f)
}

[System.IO.File]::WriteAllText($outObj, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Output "Successfully generated: $outObj ($($verts.Count) vertices, $($faces.Count) faces/mtl directives)"
