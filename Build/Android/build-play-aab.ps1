# Google Play AAB build (Earth Smasher)
# Usage: .\Build\android\build-play-aab.ps1

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not (Test-Path (Join-Path $ProjectRoot "Assets"))) {
    $ProjectRoot = "C:\Users\sunghwan\EarthCrack"
}

$LogDir = Join-Path $ProjectRoot "Build\android\logs"
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$LogFile = Join-Path $LogDir ("build-aab_{0:yyyyMMdd_HHmmss}.log" -f (Get-Date))

function Find-UnityExe {
    $hubRoot = Join-Path ${env:ProgramFiles} "Unity\Hub\Editor"
    if (-not (Test-Path $hubRoot)) {
        throw "Unity.exe not found. Install Unity Hub + Android Build Support."
    }

    $versionFile = Join-Path $ProjectRoot "ProjectSettings\ProjectVersion.txt"
    $preferred = $null
    if (Test-Path $versionFile) {
        $line = Get-Content $versionFile | Where-Object { $_ -match "^m_EditorVersion:\s*(.+)$" } | Select-Object -First 1
        if ($line -match ":\s*(.+)$") { $preferred = $Matches[1].Trim() }
    }

    if ($preferred) {
        $preferredExe = Join-Path $hubRoot "$preferred\Editor\Unity.exe"
        if (Test-Path $preferredExe) { return $preferredExe }
        Write-Warning "Project Unity $preferred not installed. Trying newest editor."
    }

    $editors = Get-ChildItem $hubRoot -Directory | Sort-Object Name -Descending
    foreach ($ed in $editors) {
        $exe = Join-Path $ed.FullName "Editor\Unity.exe"
        if (Test-Path $exe) { return $exe }
    }
    throw "Unity.exe not found. Install Unity Hub + Android Build Support."
}

$Unity = Find-UnityExe
Write-Host "Unity: $Unity"
Write-Host "Project: $ProjectRoot"
Write-Host "Log: $LogFile"

$props = Join-Path $ProjectRoot "Build\android\play-keystore.properties"
if (-not (Test-Path $props)) {
    Write-Host "No play-keystore.properties - creating upload keystore..."
    & (Join-Path $PSScriptRoot "create-upload-keystore.ps1")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$unityArgs = @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", $ProjectRoot,
    "-buildTarget", "Android",
    "-executeMethod", "AndroidBuild.BuildPlayAab",
    "-logFile", $LogFile
)
$proc = Start-Process -FilePath $Unity -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
$code = $proc.ExitCode
if ($code -eq 0) {
    $aab = Join-Path $ProjectRoot "Build\Android\Release\EarthSmasher.aab"
    if (Test-Path $aab) {
        & (Join-Path $PSScriptRoot "validate-aab.ps1") -Path $aab
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        Write-Host ""
        Write-Host "Play Console -> Create release -> Upload:"
        Write-Host $aab
    } else {
        Write-Host "ERROR: AAB not found at $aab"
        exit 4
    }
} else {
    Write-Host "BUILD FAILED (exit $code). See log:"
    Write-Host $LogFile
    if (Test-Path $LogFile) {
        Get-Content $LogFile -Tail 40
    }
    exit $code
}
