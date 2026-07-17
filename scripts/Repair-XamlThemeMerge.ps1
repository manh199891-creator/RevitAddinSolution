[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

$ErrorActionPreference = "Stop"
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptDirectory "..")).Path
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
$xamlFiles = Get-ChildItem (Join-Path $RepositoryRoot "src") -Recurse -File -Filter *.xaml |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }

$themeMarker = 'Antigravity.Core;component/UI/Themes/DesignTokens.xaml'
$overlayPath = Join-Path $RepositoryRoot 'src\Antigravity.CheckFloorElevation\UI\PenOverlayWindow.xaml'

foreach ($file in $xamlFiles) {
    $content = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)
    $updated = $content

    if ($file.FullName -eq $overlayPath) {
        $repeatedThemeBlock = '(?ms)^[ \t]*<Window\.Resources>\s*<ResourceDictionary>\s*<ResourceDictionary\.MergedDictionaries>\s*' +
            '(?:<ResourceDictionary Source="pack://application:,,,/Antigravity\.Core;component/UI/Themes/[^\"]+\.xaml"/>\s*){4}' +
            '</ResourceDictionary\.MergedDictionaries>\s*</ResourceDictionary>\s*</Window\.Resources>\s*'
        $updated = [regex]::Replace($updated, $repeatedThemeBlock, '')
    }
    elseif ($updated.Contains($themeMarker)) {
        $openCount = [regex]::Matches($updated, '<ResourceDictionary\s*>').Count
        $closeCount = [regex]::Matches($updated, '</ResourceDictionary>').Count
        if ($openCount -eq 1 -and $closeCount -eq 0) {
            $updated = [regex]::Replace(
                $updated,
                '(?m)^(?<indent>[ \t]*)</Window\.Resources>',
                '${indent}</ResourceDictionary>' + "`r`n" + '${indent}</Window.Resources>',
                1)
        }
    }

    if ($file.FullName -like '*Antigravity.ZoneSplit\UI\MainWindow.xaml') {
        $updated = [regex]::Replace(
            $updated,
            '<Button\s+Style="\{StaticResource PrimaryBtn\}"(?<attributes>[^>]*Click="BtnCancel_Click")\s+Style="\{StaticResource SecondaryBtn\}"',
            '<Button${attributes} Style="{StaticResource SecondaryBtn}"',
            1)
    }

    if ($updated -ne $content) {
        [IO.File]::WriteAllText($file.FullName, $updated, $utf8)
        Write-Host "Repaired $($file.FullName.Substring($RepositoryRoot.Length + 1))"
    }
}
