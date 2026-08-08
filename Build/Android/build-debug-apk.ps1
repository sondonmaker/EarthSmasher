# Local debug APK build
# Usage: .\Build\android\build-debug-apk.ps1

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not (Test-Path (Join-Path $ProjectRoot "Assets"))) {
    $ProjectRoot = "C:\Users\sunghwan\EarthCrack"
}

$LogDir = Join-Path $ProjectRoot "Build\android\logs"
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$LogFile = Join-Path $LogDir ("build-apk_{0:yyyyMMdd_HHmmss}.log" -f (Get-Date))

$hubRoot = Join-Path ${env:ProgramFiles} "Unity\Hub\Editor"
$preferred = $null
$versionFile = Join-Path $ProjectRoot "ProjectSettings\ProjectVersion.txt"
if (Test-Path $versionFile) {
    $line = Get-Content $versionFile | Where-Object { $_ -match "^m_EditorVersion:\s*(.+)$" } | Select-Object -First 1
    if ($line -match ":\s*(.+)$") { $preferred = $Matches[1].Trim() }
}
if ($preferred -and (Test-Path (Join-Path $hubRoot "$preferred\Editor\Unity.exe"))) {
    $Unity = Join-Path $hubRoot "$preferred\Editor\Unity.exe"
} else {
    $Unity = Get-ChildItem $hubRoot -Directory | Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName "Editor\Unity.exe" } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1
}

if (-not $Unity) { throw "Unity.exe not found" }

& $Unity `
    -batchmode -nographics -quit `
    -projectPath $ProjectRoot `
    -buildTarget Android `
    -executeMethod AndroidBuild.BuildApk `
    -logFile $LogFile

if ($LASTEXITCODE -eq 0) {
    Write-Host "SUCCESS: $ProjectRoot\Build\Android\Release\EarthSmasher.apk"
} else {
    Write-Host "FAILED - see $LogFile"
    exit $LASTEXITCODE
}
