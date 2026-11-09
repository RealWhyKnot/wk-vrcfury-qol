#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $AvatarPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [IO.Path]::GetDirectoryName($PSScriptRoot)
$packageJsonPath = Join-Path -Path $repoRoot -ChildPath 'package.json'
if (-not (Test-Path -LiteralPath $packageJsonPath)) {
    [Console]::Error.WriteLine("package.json not found at $packageJsonPath")
    exit 1
}

$package = [IO.File]::ReadAllText($packageJsonPath) | ConvertFrom-Json
$nameProperty = $package.PSObject.Properties['name']
if ($null -eq $nameProperty -or [string]::IsNullOrEmpty($nameProperty.Value)) {
    [Console]::Error.WriteLine("package.json missing 'name' field")
    exit 1
}
$packageName = [string] $nameProperty.Value

if (-not (Test-Path -LiteralPath $AvatarPath)) {
    [Console]::Error.WriteLine("Avatar project path not found: $AvatarPath")
    exit 1
}

$avatarFull = (Resolve-Path -LiteralPath $AvatarPath).Path
$packagesDirectory = Join-Path -Path $avatarFull -ChildPath 'Packages'
if (-not (Test-Path -LiteralPath $packagesDirectory)) {
    [Console]::Error.WriteLine("No Packages/ directory under $avatarFull -- not a Unity project root?")
    exit 1
}

$destination = Join-Path -Path $packagesDirectory -ChildPath $packageName
if ($destination -notmatch '\\Packages\\dev\.whyknot\.[\w\-]+\\?$') {
    [Console]::Error.WriteLine("Destination '$destination' is not under Packages\dev.whyknot.*. Refusing to deploy.")
    exit 1
}

Write-Host "Deploying $packageName"
Write-Host "  from: $repoRoot"
Write-Host "  to:   $destination"

$robocopyArguments = @(
    $repoRoot, $destination, '/MIR', '/NFL', '/NDL', '/NJH', '/R:1', '/W:1',
    '/XD', '.*', 'staging', 'scripts', (Join-Path -Path $repoRoot -ChildPath 'Tests'),
    '/XF', '.*', 'CONTRIBUTING.md', '*.zip', '*.bak', '*.log', '*.tmp', '*.out', '*.exit'
)

$null = & robocopy @robocopyArguments
if ($LASTEXITCODE -ge 8) {
    [Console]::Error.WriteLine("robocopy failed with exit code $LASTEXITCODE")
    exit 1
}
$robocopyExitCode = $LASTEXITCODE

$count = @(Get-ChildItem -LiteralPath $destination -Recurse -File).Count
Write-Host ('Deployed {0} file(s) (robocopy exit {1}).' -f $count, $robocopyExitCode)
exit 0
