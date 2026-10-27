$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Get-Samples($run) {
    $t = [System.IO.File]::ReadAllText("$root\runs\$run\samples.csv", [System.Text.Encoding]::UTF8)
    $rows = @{}
    foreach ($line in ($t -split "`n")) {
        if ($line -match '^(.+?),([\d.eE+-]+),([\d.eE+-]+),([\d.eE+-]+),') {
            $rows[$matches[1]] = @{
                'x'  = [double]$matches[2]
                'y'  = [double]$matches[3]
                'lv' = [double]$matches[4]
            }
        }
    }
    return $rows
}

$REF = @{
  'red'   = @{'x'=0.6400; 'y'=0.3300}
  'green' = @{'x'=0.3000; 'y'=0.6000}
  'blue'  = @{'x'=0.1500; 'y'=0.0600}
}
$GRAY_IDS = @('#000000','#191919','#333333','#4d4d4d','#666666','#808080','#999999','#b3b3b3','#cccccc','#e6e6e6','#ffffff')

function Signals($run, $label) {
    $s = Get-Samples $run
    $w = $s['white']
    $whiteLv = [double]$w['lv']
    Write-Host ""
    Write-Host "=== $label ($run) ==="

    # S1 black lift
    $black = [double]$s['#000000']['lv']
    Write-Host ("  S1 黑场抬升      black = {0,7:F3} cd/m2" -f $black)

    # S2 midtone (core) gamma over V in [0.25, 0.75]
    $xs = @(); $ys = @()
    foreach ($g in $GRAY_IDS) {
        if (-not $s.ContainsKey($g)) { continue }
        $b = [Convert]::ToInt32($g.Substring(1,2),16)
        $v = $b / 255.0
        if ($v -lt 0.25 -or $v -gt 0.75) { continue }
        $yn = [double]$s[$g]['lv'] / $whiteLv
        if ($yn -le 0) { continue }
        $xs += [Math]::Log($v)
        $ys += [Math]::Log($yn)
    }
    $n = $xs.Count
    $mx = 0.0; $my = 0.0
    foreach ($x in $xs) { $mx += $x }; $mx = $mx / $n
    foreach ($y in $ys) { $my += $y }; $my = $my / $n
    $sxy = 0.0; $sxx = 0.0; $syy = 0.0
    for ($i = 0; $i -lt $n; $i++) {
        $dx = $xs[$i] - $mx
        $dy = $ys[$i] - $my
        $sxy += $dx*$dy; $sxx += $dx*$dx; $syy += $dy*$dy
    }
    $slope = $sxy / $sxx
    $r2 = 0.0
    if ($syy -gt 0) { $r2 = ($sxy*$sxy) / ($sxx*$syy) }
    Write-Host ("  S2 核心区 gamma   = {0:F3}  (R2={1:F4}, n={2})   |g-2.2|={3:F3}" -f $slope, $r2, $n, [Math]::Abs($slope - 2.2))

    # S3 primary desaturation: mean pull toward white (dot / |toW|^2)
    $fr = @()
    foreach ($id in @('red','green','blue')) {
        $m = $s[$id]; $r = $REF[$id]
        $dx = [double]$m['x'] - [double]$r['x']
        $dy = [double]$m['y'] - [double]$r['y']
        $twx = [double]$w['x'] - [double]$r['x']
        $twy = [double]$w['y'] - [double]$r['y']
        $len2 = ($twx*$twx) + ($twy*$twy)
        $f = 0.0
        if ($len2 -gt 0) { $f = (($dx*$twx) + ($dy*$twy)) / $len2 }
        $fr += $f
    }
    $mean = 0.0
    foreach ($f in $fr) { $mean += $f }
    $mean = $mean / 3.0
    Write-Host ("  S3 原色去饱和    R={0,7:F2}%  G={1,7:F2}%  B={2,7:F2}%   mean={3,7:F2}%  allPos={4}" -f `
        ($fr[0]*100), ($fr[1]*100), ($fr[2]*100), ($mean*100), (($fr[0] -gt 0) -and ($fr[1] -gt 0) -and ($fr[2] -gt 0)))

    # S3a primary xy offset from sRGB (max |dxy|)
    $maxD = 0.0
    foreach ($id in @('red','green','blue')) {
        $m = $s[$id]; $r = $REF[$id]
        $dx = [double]$m['x'] - [double]$r['x']
        $dy = [double]$m['y'] - [double]$r['y']
        $d = [Math]::Sqrt(($dx*$dx) + ($dy*$dy))
        if ($d -gt $maxD) { $maxD = $d }
    }
    Write-Host ("  S3a 原色偏离sRGB  max|dxy| = {0:F4}" -f $maxD)

    # S4 white point delta u'v'
    $wx = [double]$w['x']; $wy = [double]$w['y']
    $den = (-2*$wx) + (12*$wy) + 3
    $du = 4*$wx/$den; $dv = 9*$wy/$den
    $den65 = (-2*0.3127) + (12*0.329) + 3
    $du65 = 4*0.3127/$den65; $dv65 = 9*0.329/$den65
    $duv = [Math]::Sqrt(($du-$du65)*($du-$du65) + ($dv-$dv65)*($dv-$dv65))
    Write-Host ("  S4 白点 du'v'    = {0:F4}    white Lv = {1:F2} cd/m2" -f $duv, $whiteLv)
}

Signals '20260917-161949' 'SDR baseline 1'
Signals '20260911-131058' 'SDR baseline 2'
Signals '20261019-151012' 'SDR baseline 3'
Signals '20261019-150420' 'SDR baseline 4'
Signals '20261019-150724' 'SDR baseline 5'
Signals '20261026-164853' 'HDR ON'
