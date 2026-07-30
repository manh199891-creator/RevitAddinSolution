# PLAN.md - auto_foundation_cleanup_fix

## Goal
Auto Foundation WPF UI and Logic (Note: Auto Column was previously part of this scope but has been split and moved to Antigravity.DrawColumns).

## Implementation Steps
1. Create src/Antigravity.Core/UI/AutoFoundationWindow.xaml and src/Antigravity.Core/UI/AutoFoundationWindow.xaml.cs for the WPF Window layout.
2. Create src/Antigravity.Core/UI/AutoFoundationViewModel.cs to serve as the MVVM view model binding Level lists and Structural Foundation Family lists.
3. Update src/Antigravity.Core/Commands/AutoFoundationCommand.cs implementing IExternalCommand to show AutoFoundationWindow modelessly.
4. Implement a Revit IExternalEventHandler named AutoFoundationRevitEventHandler to safely execute model transactions when UI buttons are clicked.
5. Update App.cs to correctly register btnDrawFoundation without causing encoding/mojibake issues.
6. Ensure no duplicate usings in Antigravity.DrawColumns/CreateColumnCommand.cs and that DrawColumns is completely separate.

## Test Strategy
1. Build the solution using dotnet build src/Antigravity.Core/Antigravity.Core.csproj and src/Antigravity.Main/Antigravity.Main.csproj.
2. Run dual-agent-pipeline to verify the cleanup and logic correctness.

## Rollback Procedure
If placement fails, the Transaction will catch the exception and Rollback, ensuring Revit model integrity is maintained.
