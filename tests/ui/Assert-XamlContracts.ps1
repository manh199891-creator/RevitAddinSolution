[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

$ErrorActionPreference = "Stop"
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptDirectory "..\..")).Path
}
$failures = [System.Collections.Generic.List[string]]::new()
$buttonCount = 0

function Get-DeclaredResourceKeys {
    param([string]$Content)

    $keys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    [regex]::Matches($Content, 'x:Key\s*=\s*["''](?<key>[^"'']+)["'']') |
        ForEach-Object { [void]$keys.Add($_.Groups['key'].Value) }
    return $keys
}

function Get-StaticResourceUsages {
    param([string]$Content)

    return [regex]::Matches($Content, '\{StaticResource\s+(?<key>[^\},\s]+)') |
        ForEach-Object {
            [pscustomobject]@{
                Key = $_.Groups['key'].Value
                Index = $_.Index
            }
        }
}

function Get-HandlerBody {
    param(
        [string]$Source,
        [string]$HandlerName
    )

    $escapedName = [regex]::Escape($HandlerName)
    $arrow = [regex]::Match($Source, "\b$escapedName\s*\([^\)]*\)\s*=>\s*(?<body>[^;]+);", 'Singleline')
    if ($arrow.Success) { return $arrow.Groups['body'].Value }

    $signature = [regex]::Match($Source, "\b$escapedName\s*\([^\)]*\)\s*(?<brace>\{)", 'Singleline')
    if (-not $signature.Success) { return $null }

    $start = $signature.Groups['brace'].Index
    $depth = 0
    for ($index = $start; $index -lt $Source.Length; $index++) {
        $character = $Source.Substring($index, 1)
        if ($character -eq '{') { $depth++ }
        elseif ($character -eq '}') {
            $depth--
            if ($depth -eq 0) {
                return $Source.Substring($start + 1, $index - $start - 1)
            }
        }
    }

    return $null
}

$xamlFiles = Get-ChildItem (Join-Path $RepositoryRoot "src") -Recurse -File -Filter *.xaml |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }

$themeRoot = Join-Path $RepositoryRoot 'src\Antigravity.Core\UI\Themes'
$sharedResourceKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
Get-ChildItem $themeRoot -File -Filter *.xaml | ForEach-Object {
    $themeContent = Get-Content -Raw -LiteralPath $_.FullName
    (Get-DeclaredResourceKeys -Content $themeContent) |
        ForEach-Object { [void]$sharedResourceKeys.Add($_) }
}

foreach ($file in $xamlFiles) {
    $relativePath = $file.FullName.Substring($RepositoryRoot.Length + 1)
    $content = Get-Content -Raw -LiteralPath $file.FullName

    $xmlDocument = $null
    try {
        $xmlDocument = [xml]$content
    }
    catch {
        $failures.Add("$relativePath is not valid XML: $($_.Exception.Message)")
    }

    $windowResourceCount = [regex]::Matches($content, '<Window\.Resources>').Count
    if ($windowResourceCount -gt 1) {
        $failures.Add("$relativePath declares Window.Resources $windowResourceCount times; maximum is one.")
    }

    $dictionaryContainerOpenCount = [regex]::Matches(
        $content,
        '<ResourceDictionary(?=[\s>])(?![^>]*\bSource=)[^>]*/?>').Count
    $dictionaryContainerCloseCount = [regex]::Matches($content, '</ResourceDictionary>').Count
    if ($dictionaryContainerOpenCount -ne $dictionaryContainerCloseCount) {
        $failures.Add("$relativePath has unbalanced ResourceDictionary containers ($dictionaryContainerOpenCount open/$dictionaryContainerCloseCount close).")
    }

    $themeMatches = [regex]::Matches(
        $content,
        'Antigravity\.Core;component/(?<path>UI/Themes/[^"'']+\.xaml)',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    foreach ($match in $themeMatches) {
        $themeRelativePath = $match.Groups['path'].Value -replace '/', [IO.Path]::DirectorySeparatorChar
        $themePath = Join-Path (Join-Path $RepositoryRoot 'src\Antigravity.Core') $themeRelativePath
        if (-not (Test-Path -LiteralPath $themePath)) {
            $failures.Add("$relativePath references missing theme: $themeRelativePath")
        }
    }

    if ($content -match '<Window(?=[\s>])') {
        $isOverlay = $relativePath -like '*PenOverlayWindow.xaml'
        $rootTag = [regex]::Match($content, '<Window\b[^>]*>', 'Singleline').Value

        $localResourceKeys = Get-DeclaredResourceKeys -Content $content
        $staticResourceUsages = Get-StaticResourceUsages -Content $content |
            Sort-Object Key -Unique
        foreach ($usage in $staticResourceUsages) {
            if (-not $localResourceKeys.Contains($usage.Key) -and
                -not $sharedResourceKeys.Contains($usage.Key)) {
                $failures.Add("$relativePath references unresolved StaticResource '$($usage.Key)'.")
            }
        }

        if (-not $isOverlay) {
            if ($rootTag -notmatch 'Background="#FFFFFF"') {
                $failures.Add("$relativePath must use a white Window background.")
            }
            if ($rootTag -notmatch 'FontFamily="Segoe UI"') {
                $failures.Add("$relativePath must set Segoe UI on the Window root.")
            }
            if ($rootTag -match 'ResizeMode="CanResize"') {
                if ($rootTag -notmatch 'MinWidth="') {
                    $failures.Add("$relativePath is resizable but has no MinWidth.")
                }
                if ($rootTag -notmatch 'MinHeight="') {
                    $failures.Add("$relativePath is resizable but has no MinHeight.")
                }
            }
            if ($themeMatches.Count -ne 4) {
                $failures.Add("$relativePath must merge the four shared theme dictionaries exactly once.")
            }
            if ($content -match 'FontSize="10"') {
                $failures.Add("$relativePath contains 10 DIP text; minimum readable UI text is 11 DIP.")
            }
            if ($content -match 'Foreground="#9BA3AF"') {
                $failures.Add("$relativePath uses low-contrast #9BA3AF text on the light UI.")
            }
            if ($content -match 'Background="#1A1D21"') {
                $failures.Add("$relativePath still contains the retired dark header background #1A1D21.")
            }
            $retiredLightShellStyles = [regex]::Matches(
                $content,
                '<Style\b(?![^>]*\/>)\s*[^>]*x:Key="(?<key>DarkCheck|DarkRadio|LabelStyle|HeaderStyle)"[^>]*>(?<body>[\s\S]*?)</Style>')
            foreach ($styleMatch in $retiredLightShellStyles) {
                if ($styleMatch.Groups['body'].Value -match '<Setter\s+Property="Foreground"\s+Value="White"') {
                    $failures.Add("$relativePath contains retired white-foreground style '$($styleMatch.Groups['key'].Value)' on the light shell.")
                }
            }
            if ($content -notmatch '<controls:BrandHeader\b') {
                $failures.Add("$relativePath must use the shared BrandHeader control.")
            }
            if ($content -notmatch '<controls:BrandSignature\b') {
                $failures.Add("$relativePath must use the shared BrandSignature control.")
            }
            if ($content -match '<TextBlock\b[^>]*Text="@manhns"') {
                $failures.Add("$relativePath must not implement @manhns as a raw TextBlock.")
            }
            if ($content -match '<(?:TextBlock|controls:BrandSignature)\b[^>]*(?:Margin|Padding)="[^"]*-\d') {
                $failures.Add("$relativePath uses negative positioning for the signature.")
            }
            if ($content -match '(?:Margin|Padding)="[^"]*-\d') {
                $failures.Add("$relativePath uses a negative layout offset; strict-v3 requires grid ownership instead.")
            }

            if ($relativePath -like '*Antigravity.DoorClearance\UI\ClearanceBoxWindow.xaml') {
                $widthMatch = [regex]::Match($rootTag, '\bWidth="(?<value>\d+)"')
                $heightMatch = [regex]::Match($rootTag, '\bHeight="(?<value>\d+)"')
                if (-not $widthMatch.Success -or [int]$widthMatch.Groups['value'].Value -lt 720 -or
                    -not $heightMatch.Success -or [int]$heightMatch.Groups['value'].Value -gt 650) {
                    $failures.Add("$relativePath must use the Standard Landscape profile (width >= 720, height <= 650).")
                }
            }
            if ($relativePath -like '*Antigravity.HoanThien\UI\HoanThienWindow.xaml') {
                if ($content -notmatch '<Border\s+Grid.Row="2"[^>]*>\s*<TabControl') {
                    $failures.Add("$relativePath must place its TabControl body in the star-sized row 2.")
                }
                if ($content -notmatch '<Grid\s+Grid.Row="4"[^>]*>') {
                    $failures.Add("$relativePath must place actions and signature in footer row 4.")
                }
            }
            if ($relativePath -like '*Antigravity.ArchModeling\UI\ArchModelingWindow.xaml' -and
                $content -notmatch '<Grid\s+Grid.Column="0"\s+x:Name="GridPrimaryMapping"\s+Grid.ColumnSpan="3"') {
                $failures.Add("$relativePath must let the primary mapping grid span the full result pane outside Combined mode.")
            }
        }

        $signatureMatches = [regex]::Matches($content, '<(?:TextBlock\b[^>]*Text="@manhns"|controls:BrandSignature\b)[^>]*/>', 'Singleline')
        if ($signatureMatches.Count -ne 1) {
            $failures.Add("$relativePath must contain exactly one @manhns signature.")
        }
        elseif (-not $isOverlay) {
            $signatureTag = $signatureMatches[0].Value
            if ($signatureTag -match '^<TextBlock' -and
                ($signatureTag -notmatch 'FontSize="12"' -or
                 $signatureTag -notmatch 'FontWeight="SemiBold"' -or
                 $signatureTag -notmatch 'Foreground="#6B7280"' -or
                 $signatureTag -notmatch 'HorizontalAlignment="Right"' -or
                 $signatureTag -notmatch 'IsHitTestVisible="False"')) {
                $failures.Add("$relativePath has a non-standard @manhns signature.")
            }
        }

        if ($null -ne $xmlDocument) {
            $buttonNodes = $xmlDocument.SelectNodes("//*[local-name()='Button']")
            $codeBehindPath = $file.FullName + '.cs'
            $codeBehind = if (Test-Path -LiteralPath $codeBehindPath) {
                [IO.File]::ReadAllText($codeBehindPath, [Text.Encoding]::UTF8)
            } else { '' }

            foreach ($button in $buttonNodes) {
                $buttonCount++
                $clickHandler = $button.GetAttribute('Click')
                $hasAction = -not [string]::IsNullOrWhiteSpace($clickHandler) -or
                    -not [string]::IsNullOrWhiteSpace($button.GetAttribute('Command')) -or
                    $button.GetAttribute('IsCancel') -eq 'True' -or
                    $button.GetAttribute('IsDefault') -eq 'True'
                if (-not $hasAction) {
                    $label = $button.GetAttribute('Content')
                    if ([string]::IsNullOrWhiteSpace($label)) { $label = $button.GetAttribute('Name') }
                    $failures.Add("$relativePath has a Button without an action: $label")
                }
                elseif (-not [string]::IsNullOrWhiteSpace($clickHandler) -and
                        $codeBehind -notmatch ('\b' + [regex]::Escape($clickHandler) + '\s*\(')) {
                    $failures.Add("$relativePath is missing code-behind handler: $clickHandler")
                }
                elseif (-not [string]::IsNullOrWhiteSpace($clickHandler)) {
                    $handlerBody = Get-HandlerBody -Source $codeBehind -HandlerName $clickHandler
                    if ($null -eq $handlerBody) {
                        $failures.Add("$relativePath has an unreadable/unbalanced handler body: $clickHandler")
                    }
                    else {
                        $executableBody = [regex]::Replace(
                            $handlerBody,
                            '//.*?$|/\*[\s\S]*?\*/',
                            '',
                            'Multiline').Trim()
                        if ([string]::IsNullOrWhiteSpace($executableBody) -or
                            $executableBody -match 'TODO|NotImplementedException|coming soon|chưa triển khai') {
                            $failures.Add("$relativePath has a stub button handler: $clickHandler")
                        }
                    }
                }
            }
        }
    }
}

$windowFiles = $xamlFiles | Where-Object {
    ([IO.File]::ReadAllText($_.FullName, [Text.Encoding]::UTF8)) -match '<Window(?=[\s>])'
}
if ($windowFiles.Count -ne 24) {
    $failures.Add("Expected 24 source Window XAML files but found $($windowFiles.Count).")
}
if ($buttonCount -ne 131) {
    $failures.Add("Expected 131 Window buttons but found $buttonCount; review the interaction inventory.")
}

$guidelineV3Path = Join-Path $RepositoryRoot 'docs\ui\VilaiViet_UI_Guidelines_v3.md'
if (-not (Test-Path -LiteralPath $guidelineV3Path)) {
    $failures.Add('Strict UI guideline v3 is missing.')
}

$guidelineV2Path = Join-Path $RepositoryRoot 'docs\ui\VilaiViet_UI_Guidelines_v2.md'
if (Test-Path -LiteralPath $guidelineV2Path) {
    $guidelineV2 = Get-Content -Raw -LiteralPath $guidelineV2Path
    if ($guidelineV2 -notmatch 'VilaiViet_UI_Guidelines_v3\.md') {
        $failures.Add('UI guideline v2 must point to v3 as the canonical standard.')
    }
}

if ($failures.Count -gt 0) {
    Write-Host "UI contract check FAILED with $($failures.Count) issue(s):" -ForegroundColor Red
    $failures | Sort-Object -Unique | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host "UI contract check PASSED for $($windowFiles.Count) windows, $buttonCount buttons, and $($xamlFiles.Count) total XAML files." -ForegroundColor Green
exit 0
