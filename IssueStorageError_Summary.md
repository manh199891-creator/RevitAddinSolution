# Revit BIM Issue Manager - Saving & Backup Failure Summary

This document summarizes the saving/backup issues encountered in the Revit Add-in, the root causes identified, the implemented code fixes, and the steps to deploy the updated version.

---

## 1. Current Status & Root Cause

The backup folder `C:\Users\Admin\AppData\Local\AntigravityIssueManager\Backups` is currently empty because Revit is running an **older build** of the DLL:
* **Running Build Version:** `build 2026-06-19.4` (as displayed in the UI window title).
* **Problem:** This older build does not contain the critical fixes for JSON serialization and file locking, causing the automatic backup/saving process to crash silently in the background.
* **Why it hasn't updated:** Revit locks the loaded add-in DLLs in memory. Even when Revit seems closed, ghost background processes (e.g., PID `2300`) keep locks on `Antigravity.IssueManager.dll`, preventing deployment scripts from overwriting them with the updated version.

---

## 2. Identified Bugs in the Old Build

### Bug A: DateTime Serialization Exception (UTC+7 Specific)
When serializing issues to JSON, the default `CreationDate` for legacy or uninitialized issues is `0001-01-01 00:00:00` (Local or Unspecified kind). 
* When the standard .NET `DataContractJsonSerializer` attempts to serialize it, it automatically converts it to UTC.
* Under the UTC+7 timezone, subtracting 7 hours from `0001-01-01 00:00:00` results in a date prior to `DateTime.MinValue`.
* This throws a `SerializationException` and terminates the saving process before any backup files can be written.

### Bug B: Image File Lock Exception
During the auto-save process, the add-in calls `PrepareSnapshotsForStorage` to read the newly exported snapshot images.
* Because Revit/WPF has not fully released the lock on the newly created image file, reading it throws a silent `IOException`.
* In the old build, this exception was unhandled, crashing the entire auto-save procedure.

---

## 3. Implemented Fixes in the Source Code

We have implemented and verified the fixes in the following source files:

1. **DateTime Sanitization:**
   In [IssueStorageService.cs](file:///e:/Antigravity/RevitAddinSolution/src/Antigravity.IssueManager/Services/IssueStorageService.cs#L94-L115), we updated the `SerializeIssues` method to sanitize all issue dates before serialization:
   ```csharp
   if (issue.CreationDate < new DateTime(1970, 1, 1))
   {
       issue.CreationDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
   }
   ```
2. **Snapshot Exception Handling:**
   In [IssueManagerWindow.xaml.cs](file:///e:/Antigravity/RevitAddinSolution/src/Antigravity.IssueManager/UI/IssueManagerWindow.xaml.cs#L561-L576), we wrapped `ReadSnapshotBase64` in a try-catch block to prevent file locks from crashing the save loop.
3. **Verification:**
   We created a test runner in [test_serialize.cs](file:///e:/Antigravity/RevitAddinSolution/test_serialize.cs) and successfully verified that the updated DLL handles all local, unspecified, and UTC date edge cases perfectly without throwing exceptions.

---

## 4. Steps to Deploy and Enable Backups

To deploy the new DLL and enable the backup saving system:

1. **Terminate All Revit Processes (including Ghost Processes):**
   * Open **Task Manager** (`Ctrl + Shift + Esc`).
   * Find any active or background **Revit** / `Revit.exe` processes (specifically checking for PID `2300` or other ghost instances).
   * Click **End Task** to terminate them and release the DLL file lock.
2. **Deploy the Updated DLL:**
   * Run the deployment script [DeployToRevit.ps1](file:///e:/Antigravity/RevitAddinSolution/DeployToRevit.ps1) to copy the newly compiled DLLs to the Revit add-in folder.
3. **Verify Saving:**
   * Open Revit and start the Add-in. The window title should now display `build 2026-06-19.10` (or newer).
   * Create, edit, or modify any issue. 
   * Check `C:\Users\Admin\AppData\Local\AntigravityIssueManager\Backups\` to confirm that the backup JSON files are successfully written.
