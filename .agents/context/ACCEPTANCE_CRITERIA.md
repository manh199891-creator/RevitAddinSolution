# ACCEPTANCE_CRITERIA.md

## Test Scenarios & Observable Behaviors
1. **UI Display & Data Binding:**
   - **Scenario:** The user executes AutoFoundationCommand.
   - **Expected Result:** The custom WPF window appears without default OS borders. The combobox for "Level" is populated with all Revit levels. The combobox for Family Types is populated with all loaded Structural Foundation types.

2. **Window Controls:**
   - **Scenario:** The user clicks the top header and drags the mouse.
   - **Expected Result:** The window moves across the screen. Clicking the 'X' button closes the window safely.

3. **Foundation Placement:**
   - **Scenario:** The user selects a Level, inputs a CAD layer, selects a Foundation family, clicks "Draw Foundation", and selects a CAD instance containing foundation geometries.
   - **Expected Result:** The add-in correctly creates structural foundations on the chosen Level. 

## Clean Scope Verification
- **Scenario:** Review App.cs and DrawColumns.
- **Expected Result:** App.cs should register btnDrawFoundation correctly without causing mojibake/encoding issues for other Vietnamese strings. DrawColumns files must not contain duplicated using directives. The context files must accurately reflect the AutoFoundation task, while AutoColumn is noted as split out.

## Automated and Manual Validation
- **Automated Validation:** Execute dotnet build src/Antigravity.Core/Antigravity.Core.csproj and src/Antigravity.Main/Antigravity.Main.csproj to ensure compilation succeeds.
- **Dual-Agent Verification:** The dual-agent-pipeline local script must execute and report a CODEX_STATUS of PASS.
