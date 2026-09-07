[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-GitRead {
  param([Parameter(Mandatory = $true)][string[]]$Arguments)

  $output = @(& git @Arguments 2>&1)
  if ($LASTEXITCODE -ne 0) {
    $message = ($output | ForEach-Object { [string]$_ }) -join "`n"
    throw "Git read failed: git $($Arguments -join ' ')`n$message"
  }
  return (($output | ForEach-Object { [string]$_ }) -join "`n").Trim()
}

function Invoke-GitHubCli {
  param([Parameter(Mandatory = $true)][string[]]$Arguments)

  $output = @(& gh @Arguments 2>&1)
  if ($LASTEXITCODE -ne 0) {
    $message = ($output | ForEach-Object { [string]$_ }) -join "`n"
    throw "GitHub CLI read failed: gh $($Arguments -join ' ')`n$message"
  }
  return (($output | ForEach-Object { [string]$_ }) -join "`n").Trim()
}

function Resolve-GitHubSlug {
  param([Parameter(Mandatory = $true)][string]$RemoteUrl)

  $value = $RemoteUrl.Trim()
  if ($value -match '^git@github\.com:([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+?)(?:\.git)?$') {
    return "$($Matches[1])/$($Matches[2])"
  }
  if ($value -match '^ssh://git@github\.com/([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+?)(?:\.git)?$') {
    return "$($Matches[1])/$($Matches[2])"
  }

  $uri = $null
  if ([Uri]::TryCreate($value, [UriKind]::Absolute, [ref]$uri) -and $null -ne $uri) {
    if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'github.com') {
      throw "Origin is not an https://github.com repository: $value"
    }
    if (-not [string]::IsNullOrWhiteSpace($uri.UserInfo)) {
      throw 'Origin URL contains embedded user information; refusing to echo or use it.'
    }
    $parts = @($uri.AbsolutePath.Trim('/') -split '/')
    if ($parts.Count -eq 2) {
      $owner = $parts[0]
      $repository = $parts[1] -replace '\.git$', ''
      if ($owner -match '^[A-Za-z0-9_.-]+$' -and $repository -match '^[A-Za-z0-9_.-]+$') {
        return "$owner/$repository"
      }
    }
  }

  throw 'Origin URL is not a supported github.com repository URL.'
}

if ($env:OS -ne 'Windows_NT') {
  throw 'R1 GitHub remote audit supports Windows only.'
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
  throw 'Git is required for R1 remote audit.'
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
  throw 'GitHub CLI (gh) is required for authenticated R1 remote audit.'
}

$inside = Invoke-GitRead -Arguments @('rev-parse', '--is-inside-work-tree')
if ($inside -ne 'true') {
  throw 'Current directory is not a Git working tree.'
}

$originUrl = Invoke-GitRead -Arguments @('remote', 'get-url', 'origin')
$repoSlug = Resolve-GitHubSlug -RemoteUrl $originUrl
$currentBranch = Invoke-GitRead -Arguments @('branch', '--show-current')
if ([string]::IsNullOrWhiteSpace($currentBranch)) {
  throw 'Detached HEAD is not accepted for R1 normalization.'
}
$localHead = Invoke-GitRead -Arguments @('rev-parse', 'HEAD')
if ($localHead -notmatch '^[A-Fa-f0-9]{40}$') {
  throw 'Local HEAD is not a valid Git SHA.'
}

$upstream = ''
try {
  $upstream = Invoke-GitRead -Arguments @('rev-parse', '--abbrev-ref', '--symbolic-full-name', '@{u}')
} catch {
  $upstream = ''
}

$localAheadCount = 0
$localBehindCount = 0
$aheadCommits = @()
if (-not [string]::IsNullOrWhiteSpace($upstream)) {
  $aheadText = Invoke-GitRead -Arguments @('rev-list', '--count', "$upstream..HEAD")
  $behindText = Invoke-GitRead -Arguments @('rev-list', '--count', "HEAD..$upstream")
  if (-not [int]::TryParse($aheadText, [ref]$localAheadCount)) { throw 'Could not parse local ahead count.' }
  if (-not [int]::TryParse($behindText, [ref]$localBehindCount)) { throw 'Could not parse local behind count.' }
  if ($localAheadCount -gt 0) {
    $logText = Invoke-GitRead -Arguments @('log', '--format=%H%x09%s', "$upstream..HEAD")
    $aheadCommits = @($logText -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
  }
}

$null = Invoke-GitHubCli -Arguments @('auth', 'status', '--hostname', 'github.com')
$repoMetadata = (Invoke-GitHubCli -Arguments @('api', "repos/$repoSlug")) | ConvertFrom-Json
if ($null -eq $repoMetadata) {
  throw "GitHub metadata was empty for $repoSlug."
}
$visibility = [string]$repoMetadata.visibility
$defaultBranch = [string]$repoMetadata.default_branch
if ([string]::IsNullOrWhiteSpace($defaultBranch)) {
  throw "GitHub did not return a default branch for $repoSlug."
}

$currentRef = (Invoke-GitHubCli -Arguments @('api', "repos/$repoSlug/git/ref/heads/$currentBranch")) | ConvertFrom-Json
$remoteCurrentSha = [string]$currentRef.object.sha
if ($remoteCurrentSha -notmatch '^[A-Fa-f0-9]{40}$') {
  throw "GitHub did not return a valid SHA for branch '$currentBranch'."
}

$defaultRef = (Invoke-GitHubCli -Arguments @('api', "repos/$repoSlug/git/ref/heads/$defaultBranch")) | ConvertFrom-Json
$remoteDefaultSha = [string]$defaultRef.object.sha
if ($remoteDefaultSha -notmatch '^[A-Fa-f0-9]{40}$') {
  throw "GitHub did not return a valid SHA for default branch '$defaultBranch'."
}

$localEqualsRemoteCurrent = $localHead.Equals($remoteCurrentSha, [StringComparison]::OrdinalIgnoreCase)

Write-Host 'REVIT_R1_REMOTE_AUDIT_OK'
Write-Host "Repository: $repoSlug"
Write-Host "Visibility: $visibility"
Write-Host "Private: $([bool]$repoMetadata.private)"
Write-Host "Default branch: $defaultBranch"
Write-Host "Default branch SHA: $remoteDefaultSha"
Write-Host "Local branch: $currentBranch"
Write-Host "Local HEAD: $localHead"
Write-Host "Tracked upstream: $upstream"
Write-Host "Live remote branch SHA: $remoteCurrentSha"
Write-Host "Local HEAD equals live remote branch: $localEqualsRemoteCurrent"
Write-Host "Local tracking ahead count: $localAheadCount"
Write-Host "Local tracking behind count: $localBehindCount"
if ($aheadCommits.Count -gt 0) {
  Write-Host 'Local commits ahead of tracked upstream:'
  $aheadCommits | ForEach-Object { Write-Host "  $_" }
}
