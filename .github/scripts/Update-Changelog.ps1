#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Append', 'Promote', 'Notes')]
    [string] $Mode,

    [string] $Range,
    [string] $Version,
    [switch] $ForVersion,
    [string] $Repo = $env:GITHUB_REPOSITORY,
    [string] $RepoRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Read-TextUtf8 {
    [CmdletBinding()]
    [OutputType([string])]
    param([string] $Path)

    return [System.IO.File]::ReadAllText($Path, [System.Text.UTF8Encoding]::new($false))
}

function Write-TextUtf8 {
    [CmdletBinding()]
    [OutputType([void])]
    param([string] $Path, [string] $Content)

    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Remove-BuildStamp {
    [CmdletBinding()]
    [OutputType([string])]
    param([string] $Subject)

    return ($Subject -replace ' \(\d{4}\.\d+\.\d+\.\d+(-[A-Fa-f0-9]{4,8})?\)$', '').Trim()
}

function ConvertFrom-CommitSubject {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param([string] $Sha, [string] $Subject)

    $stripped = Remove-BuildStamp -Subject $Subject
    if ([string]::IsNullOrWhiteSpace($stripped)) { return $null }
    if ($stripped -match '\[skip changelog\]') { return $null }
    if ($stripped -match '^Merge ') { return $null }

    $typePattern = '^(?<type>feat|fix|perf|refactor|docs|build|ci|chore|test|revert)'
    $pattern = $typePattern + '(?:\((?<scope>[^)]+)\))?(?<bang>!)?:\s+(?<desc>.+)$'
    $match = [regex]::Match($stripped, $pattern)
    if (-not $match.Success) {
        return @{
            Bucket = 'Changed'
            Bullet = "- $stripped (" + $Sha.Substring(0, 7) + ')'
        }
    }

    $type = $match.Groups['type'].Value
    $scope = $match.Groups['scope'].Value
    $isBreaking = $match.Groups['bang'].Success
    $description = $match.Groups['desc'].Value
    if ($description.Length -gt 0) {
        $description = $description.Substring(0, 1).ToUpper() + $description.Substring(1)
    }

    $bucket = switch ($type) {
        'feat' { 'Added' }
        'fix' { 'Fixed' }
        'perf' { 'Changed' }
        'refactor' { 'Changed' }
        'revert' { 'Changed' }
        'chore' {
            if ($scope -and $scope -match '^deps') { 'Changed' } else { $null }
        }
        default { $null }
    }

    if (-not $bucket) { return $null }
    if ($isBreaking) { $bucket = 'Breaking' }

    $scopePrefix = if ($scope) { "**${scope}:** " } else { '' }
    $shortSha = $Sha.Substring(0, 7)
    return @{ Bucket = $bucket; Bullet = "- $scopePrefix$description ($shortSha)" }
}

function Find-UnreleasedSection {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param([string] $Content)

    $lines = $Content -split "`n"
    $startIndex = -1
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -match '^##\s+Unreleased\s*$') {
            $startIndex = $i
            break
        }
    }
    if ($startIndex -lt 0) { return $null }

    $endIndex = $lines.Length
    for ($j = $startIndex + 1; $j -lt $lines.Length; $j++) {
        if ($lines[$j] -match '^---\s*$' -or $lines[$j] -match '^##\s+') {
            $endIndex = $j
            break
        }
    }

    return @{
        StartIndex = $startIndex
        EndIndex   = $endIndex
        Lines      = $lines
    }
}

function Get-SectionBodyLine {
    [CmdletBinding()]
    [OutputType([string[]])]
    param([hashtable] $Section)

    $bodyStart = $Section.StartIndex + 1
    $bodyEnd = $Section.EndIndex - 1
    if ($bodyEnd -ge $bodyStart) { return [string[]] $Section.Lines[$bodyStart..$bodyEnd] }
    return [string[]] @()
}

function Get-SectionTailLine {
    [CmdletBinding()]
    [OutputType([string[]])]
    param([hashtable] $Section)

    $lines = $Section.Lines
    if ($Section.EndIndex -lt $lines.Length) { return [string[]] $lines[$Section.EndIndex..($lines.Length - 1)] }
    return [string[]] @()
}

function ConvertFrom-UnreleasedBody {
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param([string[]] $BodyLines)

    $buckets = [ordered]@{}
    $current = $null
    foreach ($line in $BodyLines) {
        if ($line -match '^###\s+(?<name>.+?)\s*$') {
            $current = $Matches['name']
            if (-not $buckets.Contains($current)) {
                $buckets[$current] = [System.Collections.Generic.List[string]]::new()
            }
            continue
        }
        if ($line -match '^\s*- ' -and $current) {
            $buckets[$current].Add($line)
        }
    }
    return $buckets
}

function ConvertTo-UnreleasedBody {
    [CmdletBinding()]
    [OutputType([string[]])]
    param([System.Collections.IDictionary] $Buckets)

    if ($Buckets.Count -eq 0) {
        return [string[]] @('', '_No notable changes since the last release._', '')
    }

    $body = [System.Collections.Generic.List[string]]::new()
    $body.Add('')
    $emitted = @{}
    foreach ($name in @('Breaking', 'Added', 'Changed', 'Fixed')) {
        if ($Buckets.Contains($name) -and $Buckets[$name].Count -gt 0) {
            $body.Add("### $name")
            $body.AddRange([string[]] $Buckets[$name])
            $body.Add('')
            $emitted[$name] = $true
        }
    }
    foreach ($name in $Buckets.Keys) {
        if (-not $emitted.ContainsKey($name) -and $Buckets[$name].Count -gt 0) {
            $body.Add("### $name")
            $body.AddRange([string[]] $Buckets[$name])
            $body.Add('')
        }
    }
    return $body.ToArray()
}

function Test-IsPrereleaseVersion {
    [CmdletBinding()]
    [OutputType([bool])]
    param([string] $Version)

    return $Version -like '*-*'
}

function Get-VersionSection {
    [CmdletBinding()]
    [OutputType([pscustomobject[]])]
    param([string] $Content)

    $pattern = '(?ms)^##\s+\[(?<Version>[^\]]+)\][^\n]*\n(?<Body>.*?)(?=^---\s*$|^##\s+|\z)'
    $sections = foreach ($match in [regex]::Matches($Content, $pattern)) {
        [pscustomobject]@{
            Version = $match.Groups['Version'].Value
            Body    = $match.Groups['Body'].Value.Trim()
        }
    }
    return $sections
}

function Test-HasBulletForSha {
    [CmdletBinding()]
    [OutputType([bool])]
    param([string[]] $Lines, [string] $Sha)

    foreach ($line in $Lines) {
        if ($line -match "\($Sha\)") { return $true }
    }
    return $false
}

function Update-OneFile {
    [CmdletBinding()]
    [OutputType([void])]
    param([string] $Path, [hashtable] $NewBullets)

    $content = Read-TextUtf8 -Path $Path
    $section = Find-UnreleasedSection -Content $content
    if (-not $section) {
        throw "$Path is missing the '## Unreleased' section. Add a stub heading at the top before appending."
    }

    $existing = ConvertFrom-UnreleasedBody -BodyLines @(Get-SectionBodyLine -Section $section)
    foreach ($bucket in $NewBullets.Keys) {
        if (-not $existing.Contains($bucket)) {
            $existing[$bucket] = [System.Collections.Generic.List[string]]::new()
        }
        foreach ($bullet in $NewBullets[$bucket]) {
            $sha = if ($bullet -match '\(([a-f0-9]{7})\)\s*$') { $Matches[1] } else { $null }
            if ($sha -and (Test-HasBulletForSha -Lines ([string[]] $existing[$bucket]) -Sha $sha)) { continue }
            $existing[$bucket].Add($bullet)
        }
    }

    $newLines = [System.Collections.Generic.List[string]]::new()
    $newLines.AddRange([string[]] $section.Lines[0..($section.StartIndex)])
    $newLines.AddRange([string[]] @(ConvertTo-UnreleasedBody -Buckets $existing))
    $newLines.AddRange([string[]] @(Get-SectionTailLine -Section $section))
    Write-TextUtf8 -Path $Path -Content ($newLines -join "`n")
}

function Add-ChangelogEntry {
    [CmdletBinding()]
    [OutputType([void])]
    param([string] $Range, [string] $RepoRoot, [string] $ChangelogPath)

    if (-not $Range) { throw 'Append mode requires -Range (e.g. abc..def).' }

    $gitArguments = @('-C', $RepoRoot, 'log', '--no-merges', '--format=%H%x09%s%x09%ae', $Range)
    $log = & {
        $ErrorActionPreference = 'Continue'
        git @gitArguments 2>$null
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "git log returned non-zero for range '$Range' -- treating as no commits."
        return
    }
    if (-not $log) {
        Write-Host "No commits in range $Range -- nothing to append."
        return
    }

    $newBullets = @{}
    $considered = 0
    $included = 0
    foreach ($line in ($log -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split "`t"
        if ($parts.Length -lt 3) { continue }
        $considered++
        if ($parts[2] -match 'github-actions\[bot\]' -or $parts[2] -match 'noreply@github.com') { continue }

        $parsed = ConvertFrom-CommitSubject -Sha $parts[0] -Subject $parts[1]
        if (-not $parsed) { continue }

        $bucket = $parsed.Bucket
        if (-not $newBullets.ContainsKey($bucket)) {
            $newBullets[$bucket] = [System.Collections.Generic.List[string]]::new()
        }
        $newBullets[$bucket].Add($parsed.Bullet)
        $included++
    }

    Write-Host "Considered $considered commit(s), included $included in changelog."
    if ($included -eq 0) {
        Write-Host 'Nothing user-visible in this push; CHANGELOG unchanged.'
        return
    }

    Update-OneFile -Path $ChangelogPath -NewBullets $newBullets
    Write-Host "Appended $included entr(ies) to CHANGELOG.md."
}

function Test-HasReleaseContent {
    [CmdletBinding()]
    [OutputType([bool])]
    param([string[]] $BodyLines)

    foreach ($line in $BodyLines) {
        if ($line -match '^\s*- ' -or $line -match '^###\s+') { return $true }
    }
    return $false
}

function Publish-UnreleasedSection {
    [CmdletBinding()]
    [OutputType([void])]
    param([string] $Version, [string] $Repo, [string] $ChangelogPath)

    if (-not $Version) { throw 'Promote mode requires -Version (e.g. v2026.4.27.3).' }
    if (-not $Repo) { throw 'Promote mode requires -Repo or $env:GITHUB_REPOSITORY (e.g. owner/repo).' }

    $today = Get-Date -Format 'yyyy-MM-dd'
    $heading = "## [$Version](https://github.com/$Repo/releases/tag/$Version) -- $today"

    $content = Read-TextUtf8 -Path $ChangelogPath
    $section = Find-UnreleasedSection -Content $content
    if (-not $section) {
        throw "$ChangelogPath is missing the '## Unreleased' section. Cannot promote."
    }

    $bodyLines = @(Get-SectionBodyLine -Section $section)
    if (-not (Test-HasReleaseContent -BodyLines $bodyLines)) {
        $bodyLines = @('', '_Maintenance release; see commit log for details._', '')
    }

    $newLines = [System.Collections.Generic.List[string]]::new()
    if ($section.StartIndex -gt 0) {
        $newLines.AddRange([string[]] $section.Lines[0..($section.StartIndex - 1)])
    }
    $newLines.AddRange([string[]] @('## Unreleased', '', '_No notable changes since the last release._', '', '---', ''))
    $newLines.Add($heading)
    $newLines.AddRange([string[]] $bodyLines)
    $newLines.AddRange([string[]] @(Get-SectionTailLine -Section $section))

    Write-TextUtf8 -Path $ChangelogPath -Content ($newLines -join "`n")
    Write-Host "Promoted Unreleased -> $heading."
}

function Get-VersionNote {
    [CmdletBinding()]
    [OutputType([string])]
    param([string] $Content, [string] $Version)

    if (-not $Version) { throw 'Notes -ForVersion requires -Version.' }

    $sections = @(Get-VersionSection -Content $Content)
    $startIndex = -1
    for ($i = 0; $i -lt $sections.Count; $i++) {
        if ($sections[$i].Version -eq $Version) {
            $startIndex = $i
            break
        }
    }
    if ($startIndex -lt 0) { throw "No section found for $Version in CHANGELOG.md." }

    $selected = [System.Collections.Generic.List[string]]::new()
    if ($sections[$startIndex].Body) { $selected.Add($sections[$startIndex].Body) }

    if (-not (Test-IsPrereleaseVersion -Version $Version)) {
        for ($i = $startIndex + 1; $i -lt $sections.Count; $i++) {
            $section = $sections[$i]
            if (-not (Test-IsPrereleaseVersion -Version $section.Version)) { break }
            if ($section.Body) {
                $selected.Add("## $($section.Version)`n`n$($section.Body)")
            }
        }
    }

    return ($selected -join "`n`n").Trim()
}

function Get-UnreleasedNote {
    [CmdletBinding()]
    [OutputType([string])]
    param([string] $Content)

    $section = Find-UnreleasedSection -Content $Content
    if (-not $section) { throw "No '## Unreleased' section found." }
    return (@(Get-SectionBodyLine -Section $section) -join "`n").Trim()
}

if (-not $RepoRoot) {
    $RepoRoot = [IO.Path]::GetDirectoryName([IO.Path]::GetDirectoryName($PSScriptRoot))
}

$rootChangelog = Join-Path -Path $RepoRoot -ChildPath 'CHANGELOG.md'
if (-not (Test-Path -LiteralPath $rootChangelog)) { throw "CHANGELOG.md not found at $rootChangelog" }

switch ($Mode) {
    'Append' { Add-ChangelogEntry -Range $Range -RepoRoot $RepoRoot -ChangelogPath $rootChangelog }
    'Promote' { Publish-UnreleasedSection -Version $Version -Repo $Repo -ChangelogPath $rootChangelog }
    'Notes' {
        $content = Read-TextUtf8 -Path $rootChangelog
        if ($ForVersion) {
            Get-VersionNote -Content $content -Version $Version
        } else {
            Get-UnreleasedNote -Content $content
        }
    }
}
exit 0
