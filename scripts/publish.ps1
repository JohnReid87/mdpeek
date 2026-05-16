#Requires -Version 5.1
<#
.SYNOPSIS
    Build a release artifact of mdpeek.

.DESCRIPTION
    Cleans and publishes MdPeek.UI as a single-file, self-contained
    win-x64 executable using the flags recorded in docs/distribution.md, then
    drops it into ./artifacts/ as mdpeek-<version>-win-x64.exe with
    a matching .sha256 checksum file.

.PARAMETER Version
    Semantic version for the release (e.g. 0.1.0). Stamped into the assembly
    via -p:Version and used in the output filename.

.EXAMPLE
    ./scripts/publish.ps1 -Version 0.1.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$repoRoot     = Split-Path -Parent $PSScriptRoot
$uiProject    = Join-Path $repoRoot 'src/MdPeek.UI'
$artifactsDir = Join-Path $repoRoot 'artifacts'
$publishDir   = Join-Path $artifactsDir "publish-$Version-win-x64"
$finalExe     = Join-Path $artifactsDir "mdpeek-$Version-win-x64.exe"
$finalSha     = "$finalExe.sha256"

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

dotnet clean $uiProject -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet clean failed (exit $LASTEXITCODE)" }

dotnet publish $uiProject `
    -c Release `
    -r win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Version=$Version `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

if (-not (Test-Path $artifactsDir)) {
    New-Item -ItemType Directory -Path $artifactsDir | Out-Null
}

$publishedExe = Join-Path $publishDir 'MdPeek.UI.exe'
if (-not (Test-Path $publishedExe)) {
    throw "Expected published exe not found: $publishedExe"
}

Copy-Item -Path $publishedExe -Destination $finalExe -Force

$hash     = (Get-FileHash -Path $finalExe -Algorithm SHA256).Hash.ToLowerInvariant()
$fileName = Split-Path -Leaf $finalExe
# sha256sum-compatible format: "<hash>  <filename>"
"$hash  $fileName" | Set-Content -Path $finalSha -Encoding ascii

Write-Host ''
Write-Host "Published: $finalExe"
Write-Host "SHA256:    $finalSha"
Write-Host "  $hash"
