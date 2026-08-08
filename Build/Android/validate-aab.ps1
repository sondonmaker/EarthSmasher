# Validate AAB before Play Console upload
param(
    [string]$Path = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "Build\Android\Release\EarthSmasher.aab")
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $Path)) {
    Write-Host "NOT FOUND: $Path"
    Write-Host "Run: .\Build\android\build-play-aab.ps1"
    exit 1
}

$ext = [System.IO.Path]::GetExtension($Path).ToLowerInvariant()
if ($ext -ne ".aab") {
    Write-Host "INVALID: file extension is '$ext' - must be .aab"
    Write-Host "APK cannot be uploaded to the App bundle slot on Play Console."
    exit 2
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
try {
    $names = $zip.Entries | ForEach-Object { $_.FullName }
    $hasBase = $names | Where-Object {
        $_ -like "base/*" -or $_ -eq "BundleConfig.pb" -or $_ -like "*/BundleConfig.pb"
    }
    if (-not $hasBase) {
        Write-Host "INVALID AAB: missing base module - likely an APK renamed to .aab"
        exit 3
    }
} finally {
    $zip.Dispose()
}

$sizeMb = [math]::Round((Get-Item $Path).Length / 1MB, 2)
Write-Host "VALID AAB: $Path [$sizeMb MB]"
Write-Host "Upload this file to Play Console."
