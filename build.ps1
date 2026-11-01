<#
.SYNOPSIS  Test and publish Puffling as a standalone Windows executable.
.EXAMPLE   ./build.ps1                  # small exe, needs the .NET 8 Runtime installed
.EXAMPLE   ./build.ps1 -SelfContained   # bigger exe, runs on any Windows 10/11 x64 PC
#>
param(
    [switch]$SelfContained,
    [ValidateSet('win-x64', 'win-arm64')] [string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host "== Running tests" -ForegroundColor Cyan
dotnet run --project tests/Puffling.Tests -c Release
if ($LASTEXITCODE -ne 0) { throw "Tests failed - not publishing." }

$out = Join-Path $PSScriptRoot "dist/$Runtime"
Write-Host "== Publishing to $out" -ForegroundColor Cyan
dotnet publish src/Puffling.App -c Release -r $Runtime `
    --self-contained:$($SelfContained.IsPresent) `
    -p:PublishSingleFile=true `
    -p:DebugType=embedded `
    -o $out
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

Write-Host "`nDone: $out\Puffling.exe" -ForegroundColor Green
