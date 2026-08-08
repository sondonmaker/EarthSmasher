# Google Play upload key (first time only)
# Usage: .\Build\android\create-upload-keystore.ps1 [-Force]

param(
    [switch]$Force,
    [string]$StorePass,
    [string]$KeyPass
)

$ErrorActionPreference = "Stop"
$OutDir = Join-Path $PSScriptRoot "keys"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$Keystore = Join-Path $OutDir "earthsmasher-upload.keystore"
$PropsPath = Join-Path $PSScriptRoot "play-keystore.properties"
$CredsPath = Join-Path $OutDir "CREDENTIALS.txt"

if ((Test-Path $Keystore) -and -not $Force) {
    Write-Host "Keystore already exists: $Keystore"
    if (Test-Path $PropsPath) {
        Write-Host "play-keystore.properties also exists. Run build-play-aab.ps1"
        exit 0
    }
    Write-Host "Create play-keystore.properties manually or re-run with -Force"
    exit 1
}

function New-RandomPassword {
    param([int]$Length = 24)
    $chars = (48..57) + (65..90) + (97..122) + @(33, 35, 36, 37, 38, 42, 43, 45, 61, 63, 64)
    return -join ($chars | Get-Random -Count $Length | ForEach-Object { [char]$_ })
}

if (-not $StorePass) { $StorePass = New-RandomPassword }
if (-not $KeyPass) { $KeyPass = $StorePass }

$keytool = "keytool"
if (-not (Get-Command $keytool -ErrorAction SilentlyContinue)) {
    $versionFile = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "ProjectSettings\ProjectVersion.txt"
    $preferred = $null
    if (Test-Path $versionFile) {
        $line = Get-Content $versionFile | Where-Object { $_ -match "^m_EditorVersion:\s*(.+)$" } | Select-Object -First 1
        if ($line -match ":\s*(.+)$") { $preferred = $Matches[1].Trim() }
    }
    $searchRoot = if ($preferred) {
        Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$preferred"
    } else {
        Join-Path ${env:ProgramFiles} "Unity\Hub\Editor"
    }
    $jdk = Get-ChildItem $searchRoot -Recurse -Filter "keytool.exe" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($jdk) { $keytool = $jdk.FullName }
}

if (Test-Path $Keystore) { Remove-Item $Keystore -Force }

Write-Host "Creating upload keystore:"
Write-Host $Keystore

& $keytool -genkeypair -v `
    -keystore $Keystore `
    -alias upload `
    -keyalg RSA -keysize 2048 -validity 10000 `
    -storetype JKS `
    -storepass $StorePass `
    -keypass $KeyPass `
    -dname "CN=Sunsoft, OU=Mobile, O=Sunsoft, L=Seoul, ST=Seoul, C=KR"

$keystorePathForProps = ($Keystore -replace '\\', '/')
@"
keystorePath=$keystorePathForProps
keystorePass=$StorePass
keyAlias=upload
keyAliasPass=$KeyPass
"@ | Set-Content -Path $PropsPath -Encoding UTF8

@"
Earth Smasher - Google Play upload key
Created: $(Get-Date -Format o)
Keystore: $Keystore
Alias: upload
Store password: $StorePass
Key password: $KeyPass

Keep this file safe. It is gitignored.
"@ | Set-Content -Path $CredsPath -Encoding UTF8

Write-Host ""
Write-Host "Created:"
Write-Host "  $Keystore"
Write-Host "  $PropsPath"
Write-Host "  $CredsPath"
Write-Host ""
Write-Host "Next: .\Build\android\build-play-aab.ps1"
