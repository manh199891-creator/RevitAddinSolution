$files = @(
    "E:\Antigravity\RevitAddinSolution\src\Antigravity.IssueManager\UI\IssueManagerWindow.xaml",
    "E:\Antigravity\RevitAddinSolution\src\Antigravity.IssueManager\UI\CreateIssueDialog.xaml",
    "E:\Antigravity\RevitAddinSolution\src\Antigravity.IssueManager\UI\ExportIssueSelectionDialog.xaml",
    "E:\Antigravity\RevitAddinSolution\src\Antigravity.IssueManager\UI\FolderNameDialog.xaml",
    "E:\Antigravity\RevitAddinSolution\src\Antigravity.IssueManager\UI\MarkupEditorWindow.xaml"
)

foreach ($file in $files) {
    $content = Get-Content $file -Raw -Encoding UTF8

    # 1. Window Background
    $content = $content -replace 'Background="#202226"', 'Background="#FFFFFF"'
    
    # 2. Border/Card Background
    $content = $content -replace 'Background="#282B30"', 'Background="#F5F6F8"'
    $content = $content -replace 'BorderBrush="#2F333A"', 'BorderBrush="#D1D5DB"'
    $content = $content -replace 'Background="#1E1F22"', 'Background="#FFFFFF"'
    $content = $content -replace 'Background="#35393E"', 'Background="#E5E7EB"'
    
    # 3. Text colors
    $content = $content -replace 'Foreground="#F5F6F8"', 'Foreground="#000000"'
    $content = $content -replace 'Foreground="#9BA3AF"', 'Foreground="#4B5563"'
    
    # Fix brand header text colors
    $content = $content -replace '<Run Text=" · Issue Manager" Foreground="#000000" FontWeight="SemiBold"/>', '<Run Text=" · Issue Manager" Foreground="#F5F6F8" FontWeight="SemiBold"/>'
    $content = $content -replace 'Foreground="#4B5563"(\s*Margin="0,2,0,0")', 'Foreground="#9BA3AF"$1'
    
    # 4. Signature update
    $content = $content -replace '<TextBlock Text="@manhns".*?/>', '<TextBlock Text="@manhns" FontSize="16" FontWeight="Bold" Foreground="#9BA3AF" Opacity="0.8" HorizontalAlignment="Right" VerticalAlignment="Bottom"/>'
    
    Set-Content $file $content -Encoding UTF8
}
