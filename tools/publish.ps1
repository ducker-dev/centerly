<#
.SYNOPSIS
    Builds a self-contained release and packs it with Velopack: Setup.exe, portable zip and update packages.

.EXAMPLE
    ./tools/publish.ps1 -Version 0.2.0
    ./tools/publish.ps1 -Version 0.2.0 -RepositoryUrl https://github.com/user/centerly
#>
param(
    [Parameter(Mandatory)] [string] $Version,
    # GitHub repository with releases; enables in-app updates. Empty builds without update checks.
    [string] $RepositoryUrl = '',
    [string] $Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publishDir = Join-Path $root "artifacts/publish/$Runtime"
$releaseDir = Join-Path $root "artifacts/releases/$Runtime"

Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish "$root/src/Centerly.App/Centerly.App.csproj" `
    -c Release -r $Runtime --self-contained `
    -p:Version=$Version `
    -p:UpdateRepositoryUrl=$RepositoryUrl `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

dotnet tool restore
dotnet vpk pack `
    --packId Centerly `
    --packVersion $Version `
    --packTitle Centerly `
    --packAuthors ducker-lab `
    --packDir $publishDir `
    --mainExe Centerly.exe `
    --icon "$root/src/Centerly.App/Assets/app.ico" `
    --shortcuts StartMenuRoot `
    --runtime $Runtime `
    --outputDir $releaseDir
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

Write-Host "Release files: $releaseDir"
