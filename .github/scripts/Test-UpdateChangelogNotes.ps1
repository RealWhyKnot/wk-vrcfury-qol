#!/usr/bin/env pwsh
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'Update-Changelog.ps1'
$repo = Join-Path ([System.IO.Path]::GetTempPath()) ("changelog-notes-test-" + [System.Guid]::NewGuid().ToString('N'))
$encoding = [System.Text.UTF8Encoding]::new($false)

function Write-Utf8 {
    param([string]$Path, [string]$Content)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

try {
    New-Item -ItemType Directory -Path (Join-Path $repo 'wiki') -Force | Out-Null

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

    Write-Utf8 -Path (Join-Path $repo 'CHANGELOG.md') -Content $changelog
    Write-Utf8 -Path (Join-Path $repo 'wiki/Changelog.md') -Content $changelog

    $stable = (& $script -Mode Notes -ForVersion -Version 'v1.1.0' -RepoRoot $repo) -join "`n"
    if ($stable -notmatch 'Stable patch') { throw 'Stable notes did not include stable section.' }
    if ($stable -notmatch '## v1\.1\.0-beta') { throw 'Stable notes did not include beta heading.' }
    if ($stable -notmatch 'Beta patch') { throw 'Stable notes did not include beta section.' }
    if ($stable -match 'Old stable') { throw 'Stable notes included prior stable section.' }

    $beta = (& $script -Mode Notes -ForVersion -Version 'v1.1.0-beta' -RepoRoot $repo) -join "`n"
    if ($beta -notmatch 'Beta patch') { throw 'Beta notes did not include beta section.' }
    if ($beta -match 'Stable patch') { throw 'Beta notes included later stable section.' }
    if ($beta -match 'Old stable') { throw 'Beta notes included prior stable section.' }

    Write-Host 'Update-Changelog Notes tests passed.'
}
finally {
    Remove-Item -LiteralPath $repo -Recurse -Force
}
