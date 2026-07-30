# TECHNICAL_DESIGN.md

## Feature Definition
The "Auto Foundation Creator" is a WPF-based Revit add-in that automatically extracts geometric boundaries from a selected CAD ImportInstance and places corresponding Revit Structural Foundation FamilyInstances.
(Note: Auto Column feature was previously tracked here but has been split and moved to Antigravity.DrawColumns to maintain clean scope.)

## Architecture & Components
1. **AutoFoundationCommand (src/Antigravity.Core/Commands/AutoFoundationCommand.cs):**
   - Implements IExternalCommand.
   - Initializes the AutoFoundationViewModel and passes it to the AutoFoundationWindow.
   - Opens the AutoFoundationWindow modelessly.

2. **UI Controls and Bindings (src/Antigravity.Core/UI/AutoFoundationWindow.xaml & xaml.cs):**
   - **XAML View:** Flat design WPF window (No window frame, transparent background). Contains comboboxes for SelectedLevel and SelectedFamily. Includes textbox for CadLayer.
   - **Code-behind:** Handles Window.DragMove() for dragging and close button clicks. Invokes ExternalEvent for Revit API execution.

3. **View Model (src/Antigravity.Core/UI/AutoFoundationViewModel.cs):**
   - Implements INotifyPropertyChanged.
   - Dependencies: Populates AvailableLevels and Available Foundation families (OST_StructuralFoundation).

4. **RevitEventHandler (src/Antigravity.Core/Services/AutoFoundationRevitEventHandler.cs):**
   - Implements IExternalEventHandler.
   - Defines a thread-safe execution block to execute placement operations since WPF UI thread cannot mutate Revit documents.
   - Prompts user to PickObject (CAD link) and calls FoundationPlacementOrchestrator.

5. **Foundation Placement Logic (src/Antigravity.Core/Services/RevitFoundationPlacementAdapter.cs):**
   - Uses FoundationPlacementOrchestrator to extract geometries and places instances using doc.Create.NewFamilyInstance(point, familySymbol, level, StructuralType.Footing).

## Unresolved Decisions
- None. Encoding issues in App.cs have been resolved by restoring from git and applying string replacements safely using explicit UTF-8.
