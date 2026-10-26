$ErrorActionPreference = 'Continue'
$script:fail = 0
function Check($name, $cond, $detail) {
    if ($cond) { Write-Host "  OK   $name" }
    else { Write-Host "  FAIL $name  $detail"; $script:fail++ }
}
$root = Split-Path -Parent $PSScriptRoot
$readme = [System.IO.File]::ReadAllText("$root\README.md", [System.Text.Encoding]::UTF8)

Write-Host "=== 1. SDK pin ==="
$gj = [System.IO.File]::ReadAllText("$root\global.json", [System.Text.Encoding]::UTF8) | ConvertFrom-Json
Check "global.json pins 10.0.302" ($gj.sdk.version -eq '10.0.302') "actual=$($gj.sdk.version)"
Check "rollForward=latestFeature" ($gj.sdk.rollForward -eq 'latestFeature') "actual=$($gj.sdk.rollForward)"

Write-Host "=== 2. README SDK/runtime claims ==="
Check "no stale '.NET 8 SDK'" ($readme -notmatch '\.NET 8 SDK') "found"
Check "states .NET 10 SDK" ($readme -match '\*\*\.NET 10 SDK\*\*') "not found"
Check "mentions Desktop Runtime" ($readme -match 'Desktop Runtime') "not found"

Write-Host "=== 3. CLI runtimeconfig really needs WindowsDesktop.App ==="
$rc = [System.IO.File]::ReadAllText("$root\src\ColorMeasure.Host.Cli\bin\x64\Debug\net10.0-windows\color-measure.runtimeconfig.json", [System.Text.Encoding]::UTF8) | ConvertFrom-Json
$fw = $rc.runtimeOptions.frameworks.name
Check "CLI declares Microsoft.WindowsDesktop.App" ($fw -contains 'Microsoft.WindowsDesktop.App') "actual=$($fw -join ',')"

Write-Host "=== 4. native DLLs vendored in git ==="
$native = git -C $root ls-files native/
$need = @('CASDK2.dll','CASDK2Calc.dll','CASDK2Comm.dll','CASDK2Net.dll','CASDK2Socket.dll','CA200Srvr.dll')
foreach ($n in $need) { Check "vendored $n" ($native -contains "native/$n") "missing" }

Write-Host "=== 5. native DLLs copied to BOTH output paths ==="
foreach ($p in @('bin\Debug\net10.0-windows','bin\x64\Debug\net10.0-windows')) {
    $dir = "$root\src\ColorMeasure.Host.Cli\$p"
    $miss = @()
    foreach ($n in $need) { if (-not (Test-Path (Join-Path $dir $n))) { $miss += $n } }
    Check "$p has all 6 native DLLs" ($miss.Count -eq 0) "missing: $($miss -join ',')"
}

Write-Host "=== 6. CA-410 SDK present at documented path ==="
Check "CA-SDK2 x64 lib exists" (Test-Path 'C:\Program Files (x86)\KONICA MINOLTA\CA-S40\CA-SDK2\x64\lib\CASDK2Net.dll') "missing"
Check "USB driver INF present" (Test-Path 'C:\Program Files (x86)\KONICA MINOLTA\CA-S40\Driver\KMMIUSB.INF') "missing"

Write-Host "=== 7. test count claim = 62 ==="
$out = dotnet test "$root\ColorMeasure.sln" -c Debug --nologo 2>&1 | Select-String '通过:' | ForEach-Object {
    if ($_ -match '通过:\s+(\d+).*总计:\s+(\d+)') { [int]$matches[2] }
}
$total = ($out | Measure-Object -Sum).Sum
Check "62 tests total" ($total -eq 62) "actual=$total"
Check "README mentions 62" ($readme -match '62') "not found"

Write-Host "=== 8. README package list matches csproj ==="
$pkgs = @{}
Get-ChildItem $root -Recurse -Filter '*.csproj' | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } | ForEach-Object {
    $txt = [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8)
    [regex]::Matches($txt, 'PackageReference Include="([^"]+)"') | ForEach-Object { $pkgs[$_.Groups[1].Value] = $true }
}
Check "19 direct packages" ($pkgs.Count -eq 19) "actual=$($pkgs.Count)"
foreach ($p in $pkgs.Keys) {
    if ($readme -notmatch [regex]::Escape($p)) { Write-Host "  FAIL README missing package: $p"; $script:fail++ }
}

Write-Host "=== 9. no duplicated section header ==="
$navCount = ([regex]::Matches($readme, [regex]::Escape('## 文档导航'))).Count
Check "exactly one section header" ($navCount -eq 1) "actual=$navCount"

Write-Host "=== 10. typo / wording fixes ==="
Check "no 'JSD/CI'" ($readme -notmatch 'JSD/CI') "found"
Check "has 'DDC/CI'" ($readme -match 'DDC/CI') "not found"
Check "no '3 套报表'" ($readme -notmatch '3 套报表') "found"
Check "has 4 outputs" ($readme -match '4 套输出文件') "not found"

Write-Host "=== 11. doc links all resolve ==="
[regex]::Matches($readme, '\]\((docs/[^)]+)\)') | ForEach-Object {
    $p = Join-Path $root $_.Groups[1].Value
    Check "link exists: $($_.Groups[1].Value)" (Test-Path $p) "missing"
}

Write-Host ""
if ($script:fail -eq 0) { Write-Host "ALL CHECKS PASSED" } else { Write-Host "$script:fail CHECK(S) FAILED" }
