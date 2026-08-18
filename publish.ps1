#Requires -Version 7.2
param(
    [string]$Configuration = "Release",
    [string]$VersionPrefix = "1.0.0",
    [string]$VersionSuffix = "dev",
    [string]$OutputDirectory = "artifacts/publish",
    [switch]$Portable
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$publishDir = Join-Path $repoRoot $OutputDirectory

function Write-Step([string]$message) {
    Write-Host "==> $message" -ForegroundColor Cyan
}

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

New-Item -ItemType Directory -Path $publishDir | Out-Null

$commonArgs = @(
    "-c", $Configuration
    "-r", "win-x64"
    "--self-contained", "true"
    "-p:PublishSingleFile=true"
    "-p:EnableCompressionInSingleFile=true"
    "-p:VersionPrefix=$VersionPrefix"
    "-p:VersionSuffix=$VersionSuffix"
    "-p:IncludeNativeLibrariesForSelfExtract=true"
)

Write-Step "Publishing SafeFreeSpace.App"
dotnet publish (Join-Path $repoRoot "src/SafeFreeSpace.App/SafeFreeSpace.App.csproj") `
    -o (Join-Path $publishDir "app") `
    @commonArgs

Write-Step "Publishing SafeFreeSpace.ElevatedWorker"
dotnet publish (Join-Path $repoRoot "src/SafeFreeSpace.ElevatedWorker/SafeFreeSpace.ElevatedWorker.csproj") `
    -o (Join-Path $publishDir "app") `
    @commonArgs

Write-Step "Verifying manifests"
$appManifest = Join-Path $publishDir "app/SafeFreeSpace.exe.manifest"
$workerManifest = Join-Path $publishDir "app/SafeFreeSpace.ElevatedWorker.exe.manifest"

if (-not (Test-Path $appManifest)) {
    throw "App manifest not found"
}
if (-not (Test-Path $workerManifest)) {
    throw "Worker manifest not found"
}
if ((Get-Content $appManifest -Raw) -notmatch 'level="asInvoker"') {
    throw "App manifest must be asInvoker"
}
if ((Get-Content $workerManifest -Raw) -notmatch 'level="requireAdministrator"') {
    throw "Worker manifest must be requireAdministrator"
}

Write-Step "Generating checksums"
$checksums = @()
Get-ChildItem (Join-Path $publishDir "app") -File | ForEach-Object {
    $hash = Get-FileHash $_.FullName -Algorithm SHA256
    $checksums += "$($hash.Hash)  $($_.Name)"
}
$checksumPath = Join-Path $publishDir "SHA256SUMS.txt"
$checksums | Set-Content -Path $checksumPath -Encoding UTF8

if ($Portable) {
    $zipPath = Join-Path $publishDir "SafeFreeSpace-$VersionPrefix-$VersionSuffix-win-x64-portable.zip"
    Compress-Archive -Path (Join-Path $publishDir "app/*") -DestinationPath $zipPath -Force
    Write-Step "Portable archive: $zipPath"
}

Write-Step "Publish complete: $publishDir"
