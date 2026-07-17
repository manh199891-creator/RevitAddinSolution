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
$themeMarker = 'Antigravity.Core;component/UI/Themes/DesignTokens.xaml'
$themeMerge = @'
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="pack://application:,,,/Antigravity.Core;component/UI/Themes/DesignTokens.xaml"/>
                <ResourceDictionary Source="pack://application:,,,/Antigravity.Core;component/UI/Themes/Typography.xaml"/>
                <ResourceDictionary Source="pack://application:,,,/Antigravity.Core;component/UI/Themes/Controls.xaml"/>
                <ResourceDictionary Source="pack://application:,,,/Antigravity.Core;component/UI/Themes/DataControls.xaml"/>
            </ResourceDictionary.MergedDictionaries>
'@

$minimumSizes = @{
    'src\Antigravity.ArchModeling\UI\ArchModelingWindow.xaml' = @(600, 420)
    'src\Antigravity.AutoDimWalls\UI\AutoDimWindow.xaml' = @(560, 460)
    'src\Antigravity.Autojoin\UI\MainWindow.xaml' = @(720, 420)
    'src\Antigravity.CadSleevePlacer\UI\SleevePlacerWindow.xaml' = @(440, 520)
    'src\Antigravity.CadVoidPlacer\UI\MainWindow.xaml' = @(420, 500)
    'src\Antigravity.CheckFloorElevation\UI\FloorCheckerDialog.xaml' = @(860, 520)
    'src\Antigravity.DoorClearance\UI\ClashControlWindow.xaml' = @(600, 400)
    'src\Antigravity.DoorClearance\UI\ClearanceBoxWindow.xaml' = @(440, 520)
    'src\Antigravity.DrawBeams\UI\MainWindow.xaml' = @(560, 420)
    'src\Antigravity.DrawColumns\UI\MainWindow.xaml' = @(560, 420)
    'src\Antigravity.DrawFloors\UI\HatchMappingWindow.xaml' = @(480, 360)
    'src\Antigravity.DrawFloors\UI\MainWindow.xaml' = @(560, 420)
    'src\Antigravity.DrawWalls\UI\MainWindow.xaml' = @(560, 420)
    'src\Antigravity.HoanThien\UI\HoanThienWindow.xaml' = @(500, 520)
    'src\Antigravity.IssueManager\UI\CreateIssueDialog.xaml' = @(720, 480)
    'src\Antigravity.IssueManager\UI\ExportIssueSelectionDialog.xaml' = @(520, 420)
    'src\Antigravity.IssueManager\UI\IssueManagerWindow.xaml' = @(800, 520)
    'src\Antigravity.IssueManager\UI\MarkupEditorWindow.xaml' = @(720, 520)
    'src\Antigravity.TagArranger\UI\ArrangerWindow.xaml' = @(320, 640)
    'src\Antigravity.WallMepClash\UI\WallMepClashDialog.xaml' = @(860, 520)
    'src\Antigravity.ZoneSplit\UI\MainWindow.xaml' = @(420, 360)
}

$xamlFiles = Get-ChildItem (Join-Path $RepositoryRoot 'src') -Recurse -File -Filter *.xaml |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin|Themes|Controls)[\\/]' }

foreach ($file in $xamlFiles) {
    $relativePath = $file.FullName.Substring($RepositoryRoot.Length + 1)
    $content = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)
    if ($content -notmatch '<Window(?=[\s>])') { continue }

    $isOverlay = $relativePath -eq 'src\Antigravity.CheckFloorElevation\UI\PenOverlayWindow.xaml'
    $updated = $content

    if (-not $isOverlay) {
        $updated = [regex]::Replace($updated, '<Window\.Resources\b[^>]*>', '<Window.Resources>')
        if ([regex]::Matches($updated, '<ResourceDictionary(?=[\s>])(?![^>]*\bSource=)[^>]*/?>').Count -eq 0 -and
            [regex]::Matches($updated, '</ResourceDictionary>').Count -gt 0) {
            $updated = [regex]::Replace(
                $updated,
                '(?m)^[ \t]*</ResourceDictionary>\s*\r?\n(?<indent>[ \t]*)</Window\.Resources>',
                '${indent}</Window.Resources>',
                1)
        }

        $windowPattern = New-Object System.Text.RegularExpressions.Regex('<Window(?=[\s>])[^>]*>')
        $updated = $windowPattern.Replace($updated, {
            param($match)
            $root = $match.Value
            if ($root -match 'Background="[^"]+"') {
                $root = [regex]::Replace($root, 'Background="[^"]+"', 'Background="#FFFFFF"', 1)
            } else {
                $root = $root.TrimEnd('>') + ' Background="#FFFFFF">'
            }
            if ($root -notmatch 'FontFamily="') {
                $root = $root.TrimEnd('>') + ' FontFamily="Segoe UI">'
            }
            if ($root -notmatch 'UseLayoutRounding="') {
                $root = $root.TrimEnd('>') + ' UseLayoutRounding="True" SnapsToDevicePixels="True">'
            }

            if ($minimumSizes.ContainsKey($relativePath)) {
                $size = $minimumSizes[$relativePath]
                if ($root -notmatch 'ResizeMode="') {
                    $root = $root.TrimEnd('>') + ' ResizeMode="CanResize">'
                }
                if ($root -match 'ResizeMode="CanResize"') {
                    if ($root -notmatch 'MinWidth="') {
                        $root = $root.TrimEnd('>') + (' MinWidth="{0}">' -f $size[0])
                    }
                    if ($root -notmatch 'MinHeight="') {
                        $root = $root.TrimEnd('>') + (' MinHeight="{0}">' -f $size[1])
                    }
                }
            }
            return $root
        }, 1)

        if ($relativePath -eq 'src\Antigravity.HoanThien\UI\HoanThienWindow.xaml') {
            $updated = $updated.Replace('SizeToContent="Height"', 'Height="650"')
        }

        if (-not $updated.Contains($themeMarker)) {
            $updated = [regex]::Replace(
                $updated,
                '<Window\.Resources>',
                "<Window.Resources>`r`n        <ResourceDictionary>`r`n$themeMerge",
                1)
            $updated = [regex]::Replace(
                $updated,
                '(?m)^(?<indent>[ \t]*)</Window\.Resources>',
                '${indent}</ResourceDictionary>' + "`r`n" + '${indent}</Window.Resources>',
                1)
        }

        $updated = $updated.Replace('FontSize="10"', 'FontSize="11"')
        $updated = $updated.Replace('Foreground="#9BA3AF"', 'Foreground="#6B7280"')
        $updated = $updated.Replace('Foreground="#F5F6F8"', 'Foreground="#111827"')
        $updated = $updated.Replace('Background="#1A1D21"', 'Background="#FFFFFF"')
        $updated = $updated.Replace('Background="#202226"', 'Background="#FFFFFF"')
        $updated = $updated.Replace('Background="#282B30"', 'Background="#F5F6F8"')
        $updated = $updated.Replace('BorderBrush="#2F333A"', 'BorderBrush="#D1D5DB"')
        $updated = $updated.Replace('Background="#2F333A"', 'Background="#E5E7EB"')
        $updated = $updated.Replace('Background="#35393E"', 'Background="#E5E7EB"')
        $updated = $updated.Replace('Background="#E5E7EB" Foreground="White"', 'Background="#E5E7EB" Foreground="#111827"')
        $updated = $updated.Replace('x:Name="TxtSummary" Grid.Column="1" HorizontalAlignment="Right" Foreground="#FFFFFF"', 'x:Name="TxtSummary" Grid.Column="1" HorizontalAlignment="Right" Foreground="#111827"')

        $updated = [regex]::Replace($updated, '<TextBlock\b(?<attributes>[^>]*Text="@manhns"[^>]*)/>', {
            param($match)
            $attributes = $match.Groups['attributes'].Value
            foreach ($property in @('FontSize', 'FontWeight', 'Foreground', 'Opacity', 'HorizontalAlignment', 'VerticalAlignment', 'IsHitTestVisible', 'Focusable')) {
                $attributes = [regex]::Replace($attributes, '\s+' + $property + '="[^"]*"', '')
            }
            return '<TextBlock' + $attributes +
                ' FontSize="12" FontWeight="SemiBold" Foreground="#6B7280" Opacity="1"' +
                ' HorizontalAlignment="Right" VerticalAlignment="Bottom"' +
                ' IsHitTestVisible="False" Focusable="False"/>'
        })

        if ($relativePath -eq 'src\Antigravity.Core\UI\PasswordWindow.xaml') {
            $updated = $updated.Replace('<Setter Property="Foreground" Value="White" />', '<Setter Property="Foreground" Value="#111827" />')
            $updated = $updated.Replace('<Setter Property="Background"       Value="#E5E7EB"/>', '<Setter Property="Background"       Value="#F5F6F8"/>')
            $updated = $updated.Replace('<Setter Property="Foreground"       Value="White"/>', '<Setter Property="Foreground"       Value="#111827"/>')
            $updated = $updated.Replace('Background="#F5F6F8" Foreground="White" BorderBrush="#D1D5DB"', 'Background="#FFFFFF" Foreground="#111827" BorderBrush="#D1D5DB"')
            $updated = $updated.Replace('Foreground="#AAAAAA"', 'Foreground="#4B5563"')
            $updated = $updated.Replace('Foreground="#FF6666"', 'Foreground="#B91C1C"')
            $updated = $updated.Replace('Background="#FFFFFF" Margin="-16,-16,-16,20" Padding="16,12"', 'Background="#FFFFFF" BorderBrush="#D1D5DB" BorderThickness="0,0,0,1" Margin="-16,-16,-16,20" Padding="16,12"')
        }

        if ($relativePath -eq 'src\Antigravity.ZoneSplit\UI\MainWindow.xaml') {
            $updated = $updated.Replace('Background="#FFFFFF" Margin="-16,-16,-16,0" Padding="16,12"', 'Background="#FFFFFF" BorderBrush="#D1D5DB" BorderThickness="0,0,0,1" Margin="-16,-16,-16,0" Padding="16,12"')
        }
    }

    if ($updated -ne $content) {
        [IO.File]::WriteAllText($file.FullName, $updated, $utf8)
        Write-Host "Standardized $relativePath"
    }
}
