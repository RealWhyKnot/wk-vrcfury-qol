#!/usr/bin/env pwsh
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Utf8 {
    [CmdletBinding()]
    [OutputType([void])]
    param([string] $Path, [string] $Content)

    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

$script = Join-Path -Path $PSScriptRoot -ChildPath 'Update-Changelog.ps1'
$repo = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath (
    'changelog-notes-test-' + [System.Guid]::NewGuid().ToString('N'))

try {
    $null = New-Item -ItemType Directory -Path $repo -Force

    $changelog = @'
# Changelog

## Unreleased

_No notable changes since the last release._

---

## [v1.1.0](https://github.com/WhyKnot/wk-vrcfury-qol/releases/tag/v1.1.0) -- 2026-05-27

### Fixed
- Stable patch

---

## [v1.1.0-beta](https://github.com/WhyKnot/wk-vrcfury-qol/releases/tag/v1.1.0-beta) -- 2026-05-26

### Added
- Beta patch

---

## [v1.0.0](https://github.com/WhyKnot/wk-vrcfury-qol/releases/tag/v1.0.0) -- 2026-05-01

### Added
- Old stable
'@

    Write-Utf8 -Path (Join-Path -Path $repo -ChildPath 'CHANGELOG.md') -Content $changelog

    $stable = (& $script -Mode Notes -ForVersion -Version 'v1.1.0' -RepoRoot $repo) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Stable notes exited with $LASTEXITCODE." }
    if ($stable -notmatch 'Stable patch') { throw 'Stable notes did not include stable section.' }
    if ($stable -notmatch '## v1\.1\.0-beta') { throw 'Stable notes did not include beta heading.' }
    if ($stable -notmatch 'Beta patch') { throw 'Stable notes did not include beta section.' }
    if ($stable -match 'Old stable') { throw 'Stable notes included prior stable section.' }

    $beta = (& $script -Mode Notes -ForVersion -Version 'v1.1.0-beta' -RepoRoot $repo) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Beta notes exited with $LASTEXITCODE." }
    if ($beta -notmatch 'Beta patch') { throw 'Beta notes did not include beta section.' }
    if ($beta -match 'Stable patch') { throw 'Beta notes included later stable section.' }
    if ($beta -match 'Old stable') { throw 'Beta notes included prior stable section.' }

    Write-Host 'Update-Changelog Notes tests passed.'
} finally {
    Remove-Item -LiteralPath $repo -Recurse -Force
}
exit 0
