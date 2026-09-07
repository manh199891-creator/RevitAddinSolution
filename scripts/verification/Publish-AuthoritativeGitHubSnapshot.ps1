[CmdletBinding()]
param(
  [string]$Remote = 'origin',
  [string]$Branch = 'main',
  [string]$CommitMessage = 'chore: sync authoritative RevitAddinSolution snapshot'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Git {
  param([Parameter(Mandatory = $true)][string[]]$Arguments)
  $previousErrorAction = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try {
    $output = @(& git @Arguments 2>&1 | ForEach-Object { [string]$_ })
    $exitCode = $LASTEXITCODE
  }
  finally {
    $ErrorActionPreference = $previousErrorAction
  }
  if ($exitCode -ne 0) {
    $message = $output -join "`n"
    throw "Git command failed: git $($Arguments -join ' ')`n$message"
  }
  return ($output -join "`n").Trim()
}

function Invoke-GitHubCli {
  param([Parameter(Mandatory = $true)][string[]]$Arguments)
  $previousErrorAction = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try {
    $output = @(& gh @Arguments 2>&1 | ForEach-Object { [string]$_ })
    $exitCode = $LASTEXITCODE
  }
  finally {
    $ErrorActionPreference = $previousErrorAction
  }
  if ($exitCode -ne 0) {
    $message = $output -join "`n"
    throw "GitHub CLI command failed: gh $($Arguments -join ' ')`n$message"
  }
  return ($output -join "`n").Trim()
}

function Resolve-GitHubSlug {
  param([Parameter(Mandatory = $true)][string]$RemoteUrl)
  $value = $RemoteUrl.Trim()
  if ($value -match '^git@github\.com:([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+?)(?:\.git)?$') {
    return "$($Matches[1])/$($Matches[2])"
  }
  $uri = $null
  if ([Uri]::TryCreate($value, [UriKind]::Absolute, [ref]$uri) -and $null -ne $uri) {
    if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'github.com' -or -not [string]::IsNullOrWhiteSpace($uri.UserInfo)) {
      throw 'Origin is not a supported credential-free github.com URL.'
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
  throw 'Origin is not a supported github.com repository URL.'
}

function Test-ExcludedPath {
  param([Parameter(Mandatory = $true)][string]$Path)
  $p = $Path.Replace('\\','/')
  if ($p -match '(^|/)\.ai-bridge(/|$)') { return $true }
  if ($p -match '(^|/)\.agent/(context|state|reports)(/|$)') { return $true }
  if ($p -match '(^|/)\.agents/(context|factory)(/|$)') { return $true }
  if ($p -match '(^|/)\.pytest_cache(/|$)') { return $true }
  if ($p -match '(^|/)__pycache__(/|$)') { return $true }
  if ($p -match '\.(pyc|pyo)$') { return $true }
  if ($p -match '(^|/)(bin|obj|TestResults)(/|$)') { return $true }
  if ($p -match '(^|/)smoke-tests/results/' -and $p -notmatch '/\.gitkeep$') { return $true }
  if ($p -match '^artifacts(/|$)') { return $true }
  if ($p -match '^(_Deploy|_packages|_addin_manager|_Installer|TestFiles)(/|$)') { return $true }
  if ($p -match '(^|/)(logs|\.tmp)(/|$)') { return $true }
  if ($p -match '\.(log|tmp|bak|nupkg|snupkg)$') { return $true }
  if ([System.IO.Path]::GetFileName($p) -like '~$*') { return $true }
  if ($p -match '^source-code(/|$)') { return $true }
  return $false
}

function Test-BlockedSensitivePath {
  param([Parameter(Mandatory = $true)][string]$Path)
  $name = [System.IO.Path]::GetFileName($Path)
  if ($name -match '^\.env(?:\..+)?$') { return $true }
  if ($name -match '\.(pfx|p12|pem|key)$') { return $true }
  if ($name -match '(?i)(credentials|secrets?)') { return $true }
  return $false
}

if ($env:OS -ne 'Windows_NT') { throw 'Windows only.' }
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Git is required.' }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI (gh) is required.' }

$repoRoot = Invoke-Git -Arguments @('rev-parse','--show-toplevel')
Set-Location -LiteralPath $repoRoot
$originUrl = Invoke-Git -Arguments @('remote','get-url',$Remote)
$repoSlug = Resolve-GitHubSlug -RemoteUrl $originUrl
$null = Invoke-GitHubCli -Arguments @('auth','status','--hostname','github.com')
$repoMetadata = (Invoke-GitHubCli -Arguments @('api',"repos/$repoSlug")) | ConvertFrom-Json
if ($null -eq $repoMetadata) { throw "GitHub metadata was empty for $repoSlug." }
if ([string]$repoMetadata.default_branch -ne $Branch) {
  throw "Expected GitHub default branch '$Branch' but live repository reports '$($repoMetadata.default_branch)'."
}

$remoteRef = (Invoke-GitHubCli -Arguments @('api',"repos/$repoSlug/git/ref/heads/$Branch")) | ConvertFrom-Json
$expectedRemoteSha = [string]$remoteRef.object.sha
if ($expectedRemoteSha -notmatch '^[A-Fa-f0-9]{40}$') { throw 'GitHub did not return a valid remote branch SHA.' }

$originalIndex = $env:GIT_INDEX_FILE
$tempIndex = Join-Path $env:TEMP ("revit-r1-index-{0}.tmp" -f [guid]::NewGuid().ToString('N'))
$env:GIT_INDEX_FILE = $tempIndex

try {
  Invoke-Git -Arguments @('read-tree','HEAD') | Out-Null
  Invoke-Git -Arguments @('add','-A','--','.') | Out-Null

  $allPaths = @((Invoke-Git -Arguments @('ls-files')) -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
  $excluded = @($allPaths | Where-Object { Test-ExcludedPath -Path $_ })
  foreach ($path in $excluded) {
    & git rm -q --cached --ignore-unmatch -- $path 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Failed to exclude transient/generated path from snapshot: $path" }
  }

  $snapshotPaths = @((Invoke-Git -Arguments @('ls-files')) -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
  $blockedSensitivePaths = @($snapshotPaths | Where-Object { Test-BlockedSensitivePath -Path $_ })
  if ($blockedSensitivePaths.Count -gt 0) {
    throw "Sensitive path names are present in snapshot: $($blockedSensitivePaths -join ', ')"
  }

  $oversize = New-Object System.Collections.Generic.List[string]
  foreach ($path in $snapshotPaths) {
    $full = Join-Path $repoRoot $path
    if (Test-Path -LiteralPath $full -PathType Leaf) {
      $size = (Get-Item -LiteralPath $full).Length
      if ($size -gt 95MB) { $oversize.Add("$path ($size bytes)") }
    }
  }
  if ($oversize.Count -gt 0) {
    throw "Snapshot contains files larger than 95 MiB: $($oversize -join ', ')"
  }

  $providerPrefix = 'gh' + 'p_'
  $fineGrainedPrefix = 'github_' + 'pat_'
  $cloudPrefix = 'AK' + 'IA'
  $privateKeyMarker = 'BEGIN ' + 'PRIVATE KEY'
  $bridgeTokenMarker = 'codexpro_' + 'token='
  $modelKeyPrefix = 's' + 'k-'
  $sensitiveRegex = "($providerPrefix[A-Za-z0-9]{20,}|$fineGrainedPrefix[A-Za-z0-9_]{20,}|$cloudPrefix[0-9A-Z]{16}|$privateKeyMarker|$bridgeTokenMarker[A-Za-z0-9]{16,}|$modelKeyPrefix[A-Za-z0-9_-]{20,})"
  $sensitiveMatches = @(& git grep --cached -I -l -E $sensitiveRegex -- . 2>$null)
  $grepExit = $LASTEXITCODE
  if ($grepExit -eq 0 -and $sensitiveMatches.Count -gt 0) {
    throw "Potential credential material detected in snapshot files: $($sensitiveMatches -join ', ')"
  }
  if ($grepExit -ne 0 -and $grepExit -ne 1) { throw 'Credential scan failed.' }

  $treeSha = Invoke-Git -Arguments @('write-tree')
  if ($treeSha -notmatch '^[A-Fa-f0-9]{40}$') { throw 'git write-tree did not return a valid tree SHA.' }

  $currentTree = Invoke-Git -Arguments @('rev-parse',"$expectedRemoteSha^{tree}")
  if ($treeSha.Equals($currentTree,[StringComparison]::OrdinalIgnoreCase)) {
    Write-Host 'REVIT_AUTHORITATIVE_SNAPSHOT_ALREADY_CURRENT'
    Write-Host "Repository: $repoSlug"
    Write-Host "Remote: $Remote/$Branch = $expectedRemoteSha"
    Write-Host "Snapshot tree: $treeSha"
    exit 0
  }

  $commitSha = ($CommitMessage | & git commit-tree $treeSha -p $expectedRemoteSha).Trim()
  if ($LASTEXITCODE -ne 0 -or $commitSha -notmatch '^[A-Fa-f0-9]{40}$') {
    throw 'Failed to create authoritative snapshot commit.'
  }

  $previousErrorAction = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try {
    $pushOutput = @(& git push $Remote "$commitSha`:refs/heads/$Branch" 2>&1 | ForEach-Object { [string]$_ })
    $pushExitCode = $LASTEXITCODE
  }
  finally {
    $ErrorActionPreference = $previousErrorAction
  }
  if ($pushExitCode -ne 0) {
    $message = $pushOutput -join "`n"
    throw "Normal fast-forward push failed. Remote may have moved; no force was attempted.`n$message"
  }

  $verifiedRef = (Invoke-GitHubCli -Arguments @('api',"repos/$repoSlug/git/ref/heads/$Branch")) | ConvertFrom-Json
  $verifiedSha = [string]$verifiedRef.object.sha
  if (-not $verifiedSha.Equals($commitSha,[StringComparison]::OrdinalIgnoreCase)) {
    throw "Remote verification failed: expected $commitSha but GitHub reports $verifiedSha."
  }

  Write-Host 'REVIT_AUTHORITATIVE_SNAPSHOT_PUBLISHED'
  Write-Host "Repository: $repoSlug"
  Write-Host "Visibility: $($repoMetadata.visibility)"
  Write-Host "Branch: $Branch"
  Write-Host "Previous remote SHA: $expectedRemoteSha"
  Write-Host "Published commit: $commitSha"
  Write-Host "Snapshot tree: $treeSha"
  Write-Host "Excluded transient/generated paths: $($excluded.Count)"
  Write-Host "Published files: $($snapshotPaths.Count)"
}
finally {
  if ($null -eq $originalIndex) { Remove-Item Env:GIT_INDEX_FILE -ErrorAction SilentlyContinue }
  else { $env:GIT_INDEX_FILE = $originalIndex }
  Remove-Item -LiteralPath $tempIndex -Force -ErrorAction SilentlyContinue
  Remove-Item -LiteralPath "$tempIndex.lock" -Force -ErrorAction SilentlyContinue
}
