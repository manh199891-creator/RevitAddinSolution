[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

$utf8NoBom = [Text.UTF8Encoding]::new($false)
$sourceRoot = Join-Path $RepositoryRoot 'src'
$headerPattern = '(?s)<Border(?<attrs>[^>]*)>(?:(?!</Border>).)*?M 30,10 L 70,10(?:(?!</Border>).)*?</Border>'
$signaturePattern = '(?s)<TextBlock\b(?<attrs>[^>]*\bText="@manhns"[^>]*)/>'
$changed = [System.Collections.Generic.List[string]]::new()

function Get-AttachedGridAttributes {
    param([string]$Attributes)

    $result = [System.Collections.Generic.List[string]]::new()
    foreach ($name in @('Grid.Row', 'Grid.Column', 'Grid.RowSpan', 'Grid.ColumnSpan')) {
        $attributePattern = '\b{0}="(?<value>[^"]+)"' -f [regex]::Escape($name)
        $match = [regex]::Match($Attributes, $attributePattern)
        if ($match.Success) {
            $result.Add(('{0}="{1}"' -f $name, $match.Groups['value'].Value))
        }
    }
    return $result -join ' '
}

function Escape-XamlAttribute {
    param([string]$Value)

    return [Security.SecurityElement]::Escape($Value)
}

$windowFiles = Get-ChildItem $sourceRoot -Recurse -File -Filter '*.xaml' |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }

foreach ($file in $windowFiles) {
    $content = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)
    if ($content -notmatch '<Window(?=[\s>])') { continue }

    $updated = $content
    if ($updated -notmatch 'xmlns:controls=') {
        $controlsNamespace = if ($file.FullName -like '*\Antigravity.Core\*') {
            'clr-namespace:Antigravity.Core.UI.Controls'
        } else {
            'clr-namespace:Antigravity.Core.UI.Controls;assembly=Antigravity.Core'
        }
        $updated = [regex]::Replace(
            $updated,
            '(xmlns:x="http://schemas\.microsoft\.com/winfx/2006/xaml")',
            '$1' + [Environment]::NewLine + '        xmlns:controls="' + $controlsNamespace + '"',
            1)
    }
    if ($updated -match $headerPattern) {
        $updated = [regex]::Replace($updated, $headerPattern, {
            param($match)

            $windowTitle = [regex]::Match($content, '<Window\b[^>]*\bTitle="(?<title>[^"]*)"', 'Singleline').Groups['title'].Value
            $moduleTitle = $windowTitle -replace '^(?i:Vilai Viet)\s*(?:[-–—:·]\s*)?', ''
            if ([string]::IsNullOrWhiteSpace($moduleTitle)) { $moduleTitle = $windowTitle }

            $subtitleMatch = [regex]::Match($match.Value, '<TextBlock\b[^>]*\bText="(?<subtitle>[^"]*)"', 'Singleline')
            $subtitle = if ($subtitleMatch.Success) { $subtitleMatch.Groups['subtitle'].Value } else { '' }
            $gridAttributes = Get-AttachedGridAttributes -Attributes $match.Groups['attrs'].Value
            if (-not [string]::IsNullOrWhiteSpace($gridAttributes)) { $gridAttributes = ' ' + $gridAttributes }

            return '<controls:BrandHeader' + $gridAttributes +
                ' TitleText="' + (Escape-XamlAttribute $moduleTitle) +
                '" SubtitleText="' + (Escape-XamlAttribute $subtitle) + '"/>'
        }, 1)
    }

    $isOverlay = $file.Name -eq 'PenOverlayWindow.xaml'
    if (-not $isOverlay -and $updated -match $signaturePattern) {
        $updated = [regex]::Replace($updated, $signaturePattern, {
            param($match)
            $gridAttributes = Get-AttachedGridAttributes -Attributes $match.Groups['attrs'].Value
            if (-not [string]::IsNullOrWhiteSpace($gridAttributes)) { $gridAttributes = ' ' + $gridAttributes }
            return '<controls:BrandSignature' + $gridAttributes + ' Margin="0,8,0,0"/>'
        }, 1)
    }

    if ($updated -ne $content) {
        [xml]$null = $updated
        [IO.File]::WriteAllText($file.FullName, $updated, $utf8NoBom)
        $changed.Add($file.FullName.Substring($RepositoryRoot.Length + 1))
    }
}

Write-Host "Strict-v3 migration updated $($changed.Count) Window XAML files."
$changed | Sort-Object | ForEach-Object { Write-Host " - $_" }
