$ErrorActionPreference = 'Stop'

# ===== 请把这一行改为你自己的 WorldBox 安装根目录 =====
$gameRoot = 'D:\steam files\steamapps\common\worldbox'
# =====================================================

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $scriptDir 'src'
$managed = Join-Path $gameRoot 'worldbox_Data\Managed'
$nml = Join-Path $gameRoot 'worldbox_Data\StreamingAssets\mods\NeoModLoader.dll'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $scriptDir 'PowerRanking.dll'

if (-not (Test-Path $csc)) {
    throw "csc.exe not found: $csc"
}

$refs = @(
    (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'UnityEngine.UIModule.dll'),
    (Join-Path $managed 'UnityEngine.UI.dll'),
    (Join-Path $managed 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $managed 'UnityEngine.InputLegacyModule.dll'),
    (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $managed 'netstandard.dll'),
    (Join-Path $managed 'Newtonsoft.Json.dll'),
    $nml
)

$sources = Get-ChildItem -Path $srcDir -Filter '*.cs' | Select-Object -ExpandProperty FullName

$args = @(
    '/target:library',
    '/optimize+',
    '/platform:anycpu',
    "/out:$out"
)

foreach ($r in $refs) {
    $args += "/r:$r"
}

foreach ($s in $sources) {
    $args += $s
}

Write-Output "Compiling..."
& $csc @args
if ($LASTEXITCODE -ne 0) {
    throw "csc failed with exit code $LASTEXITCODE"
}

Write-Output "Built: $out"

# Install as an NML mod into the top-level Mods folder.
$modFolder = Join-Path $gameRoot 'Mods\PowerRanking'
if (-not (Test-Path $modFolder)) {
    New-Item -ItemType Directory -Path $modFolder -Force | Out-Null
}
Copy-Item -Path $out -Destination (Join-Path $modFolder 'PowerRanking.dll') -Force
Copy-Item -Path (Join-Path $scriptDir 'mod.json') -Destination (Join-Path $modFolder 'mod.json') -Force
Copy-Item -Path (Join-Path $scriptDir 'icon.png') -Destination (Join-Path $modFolder 'icon.png') -Force
Copy-Item -Path (Join-Path $scriptDir 'PowerRanking.weights.txt') -Destination (Join-Path $modFolder 'PowerRanking.weights.txt') -Force
Write-Output "Installed to: $modFolder"

# Remove the old built-in-loader DLL so it does not double-load.
$old = Join-Path $gameRoot 'worldbox_Data\StreamingAssets\mods\PowerRanking_memload.dll'
if (Test-Path $old) {
    Remove-Item -LiteralPath $old -Force
    Write-Output "Removed old built-in mod: $old"
}
