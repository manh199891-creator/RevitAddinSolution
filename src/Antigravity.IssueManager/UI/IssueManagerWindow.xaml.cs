using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Antigravity.IssueManager.Models;
using Antigravity.IssueManager.Services;
using Antigravity.IssueManager.Handlers;
using Autodesk.Revit.UI;
using System.Reflection;
using DB = Autodesk.Revit.DB;

namespace Antigravity.IssueManager.UI
{
    public partial class IssueManagerWindow : Window
    {
        private UIApplication _uiApp;
        private List<IssueModel> _currentIssues = new List<IssueModel>();
        private IssueModel _selectedIssue;

        private CreateIssueHandler _createIssueHandler;
        private ExternalEvent _createIssueEvent;
        private ShowIssueInModelHandler _showIssueHandler;
        private ExternalEvent _showIssueEvent;
        private IsolateClashElementsHandler _isolateClashHandler;
        private ExternalEvent _isolateClashEvent;
        private ModelClashPointHandler _modelClashHandler;
        private ExternalEvent _modelClashEvent;
        private VerifyClashHandler _verifyClashHandler;
        private ExternalEvent _verifyClashEvent;
        private SaveIssuesHandler _saveIssuesHandler;
        private ExternalEvent _saveIssuesEvent;
        private bool _isUpdatingStatusCombo;
        private bool _isUpdatingFolderCombo;
        private string _lastXmlDiagnosticPath;
        private readonly HashSet<string> _knownFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private CreateIssueDialog _createIssueDialog;

        public IssueManagerWindow(UIApplication uiApp, 
            CreateIssueHandler createIssueHandler, ExternalEvent createIssueEvent,
            ShowIssueInModelHandler showIssueHandler, ExternalEvent showIssueEvent,
            IsolateClashElementsHandler isolateClashHandler, ExternalEvent isolateClashEvent,
            ModelClashPointHandler modelClashHandler, ExternalEvent modelClashEvent,
            VerifyClashHandler verifyClashHandler, ExternalEvent verifyClashEvent,
            SaveIssuesHandler saveIssuesHandler, ExternalEvent saveIssuesEvent)
        {
            InitializeComponent();
            _uiApp = uiApp;
            _createIssueHandler = createIssueHandler;
            _createIssueEvent = createIssueEvent;
            _showIssueHandler = showIssueHandler;
            _showIssueEvent = showIssueEvent;
            _isolateClashHandler = isolateClashHandler;
            _isolateClashEvent = isolateClashEvent;
            _modelClashHandler = modelClashHandler;
            _modelClashEvent = modelClashEvent;
            _verifyClashHandler = verifyClashHandler;
            _verifyClashEvent = verifyClashEvent;
            _saveIssuesHandler = saveIssuesHandler;
            _saveIssuesEvent = saveIssuesEvent;

            _createIssueHandler.OnIssueCreated = OnIssueCreated;
            _createIssueHandler.OnCaptured = OnView3DCaptured;
            _createIssueHandler.OnError = OnCreateIssueError;
            _verifyClashHandler.OnVerified = OnClashVerified;
            _verifyClashHandler.OnError = OnVerifyClashError;
            _saveIssuesHandler.OnError = OnSaveIssuesError;

            Loaded += IssueManagerWindow_Loaded;
        }

        private void OnView3DCaptured(string snapshotPath, string levelName)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (_createIssueDialog != null && _createIssueDialog.IsLoaded)
                        {
                            _createIssueDialog.HandleCaptured3DView(snapshotPath, levelName);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi trong HandleCaptured3DView:\n{ex}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi trong Dispatcher.Invoke:\n{ex}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IssueManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Title = "Issue Manager - build 2026-06-19.10";
            LogRuntimeAssemblyInfo();
            AutoLoadIssues();
            UpdateDashboardSummary();
        }

        private void UpdateDashboardSummary()
        {
            if (_currentIssues == null)
            {
                TxtTotal.Text = "Total: 0";
                TxtNew.Text = "New: 0";
                TxtActive.Text = "Active: 0";
                TxtResolved.Text = "Resolved: 0";
                TxtApproved.Text = "Approved: 0";
                return;
            }

            int total = _currentIssues.Count;
            int newCount = 0;
            int activeCount = 0;
            int resolvedCount = 0;
            int approvedCount = 0;

            foreach (var issue in _currentIssues)
            {
                string status = issue.Status?.ToLower() ?? "";
                if (status.Contains("new")) newCount++;
                else if (status.Contains("active")) activeCount++;
                else if (status.Contains("resolved")) resolvedCount++;
                else if (status.Contains("approved")) approvedCount++;
            }

            TxtTotal.Text = $"Total: {total}";
            TxtNew.Text = $"New: {newCount}";
            TxtActive.Text = $"Active: {activeCount}";
            TxtResolved.Text = $"Resolved: {resolvedCount}";
            TxtApproved.Text = $"Approved: {approvedCount}";
        }

        private void RefreshIssueViews()
        {
            SyncKnownFoldersFromIssues();
            IssueModel selected = _selectedIssue;

            LvIssues.ItemsSource = null;
            LvIssues.ItemsSource = _currentIssues;
            if (selected != null && _currentIssues.Contains(selected))
            {
                LvIssues.SelectedItem = selected;
            }

            TvIssueGroups.ItemsSource = null;
            TvIssueGroups.ItemsSource = BuildIssueGroups();
        }

        private List<IssueGroupModel> BuildIssueGroups()
        {
            if (_currentIssues == null)
            {
                return new List<IssueGroupModel>();
            }

            bool groupByFolder = CmbViewMode.SelectedIndex == 2;

            if (groupByFolder)
            {
                List<IssueGroupModel> folderGroups = _knownFolders
                    .OrderBy(folder => folder)
                    .Select(folder => new IssueGroupModel
                    {
                        GroupName = folder,
                        Issues = _currentIssues
                            .Where(issue => string.Equals(GetIssueFolderName(issue), folder, StringComparison.OrdinalIgnoreCase))
                            .ToList()
                    })
                    .ToList();

                if (!folderGroups.Any(group => string.Equals(group.GroupName, "Uncategorized", StringComparison.OrdinalIgnoreCase)))
                {
                    folderGroups.Insert(0, new IssueGroupModel
                    {
                        GroupName = "Uncategorized",
                        Issues = _currentIssues
                            .Where(issue => string.Equals(GetIssueFolderName(issue), "Uncategorized", StringComparison.OrdinalIgnoreCase))
                            .ToList()
                    });
                }

                return folderGroups;
            }
            else
            {
                return _currentIssues
                    .SelectMany(issue => GetIssueElementIds(issue).Select(id => new { id, issue }))
                    .GroupBy(x => x.id)
                    .Where(group => group.Count() > 1)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key)
                    .Select(group => new IssueGroupModel
                    {
                        GroupName = $"Element {group.Key}",
                        Issues = group.Select(x => x.issue).Distinct().ToList()
                    })
                    .ToList();
            }
        }

        private void SyncKnownFoldersFromIssues()
        {
            _knownFolders.Add("Uncategorized");
            if (_currentIssues == null) return;

            foreach (IssueModel issue in _currentIssues)
            {
                string folder = GetIssueFolderName(issue);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    _knownFolders.Add(folder);
                }
            }
        }

        private static string GetIssueFolderName(IssueModel issue)
        {
            return string.IsNullOrWhiteSpace(issue?.Folder) ? "Uncategorized" : issue.Folder.Trim();
        }

        private static IEnumerable<string> GetIssueElementIds(IssueModel issue)
        {
            if (issue?.Viewpoint?.ElementIds == null)
            {
                yield break;
            }

            foreach (string elementId in issue.Viewpoint.ElementIds)
            {
                if (!string.IsNullOrWhiteSpace(elementId))
                {
                    yield return elementId.Trim();
                }
            }
        }

        private void SelectIssue(IssueModel issue)
        {
            _selectedIssue = issue;

            if (issue == null)
            {
                TxtTitle.Text = "Select an issue...";
                TxtDesc.Text = "";
                TxtAuthor.Text = "";
                ImgSnapshot.Source = null;
                ImgSnapshot2.Source = null;
                TxtClashFiles.Text = "";
                SetStatusCombo(null);
                CmbIssueStatus.IsEnabled = false;
                
                CmbIssueFolder.ItemsSource = null;
                CmbIssueFolder.Text = "";
                CmbIssueFolder.IsEnabled = false;
                BtnCreateFolderForIssue.IsEnabled = false;
                BtnMarkupSnapshot3D.Visibility = Visibility.Collapsed;
                BtnMarkupSnapshot2D.Visibility = Visibility.Collapsed;
                BtnEditIssue.IsEnabled = false;
                return;
            }

            TxtTitle.Text = issue.DisplayTitle;
            TxtDesc.Text = issue.Description;
            string levelText = string.IsNullOrWhiteSpace(issue.Level) ? string.Empty : $" | Level: {issue.Level}";
            string assignedText = string.IsNullOrWhiteSpace(issue.AssignedTo) ? string.Empty : $" | Assigned: {issue.AssignedTo}";
            TxtAuthor.Text = $"By {issue.Author} on {issue.CreationDate.ToShortDateString()}{levelText}{assignedText} | Dist: {issue.Distance}";
            TxtClashFiles.Text = FormatClashFileStatus(issue);
            SetStatusCombo(issue.Status);
            CmbIssueStatus.IsEnabled = true;
            BtnEditIssue.IsEnabled = true;

            PopulateFolderCombo();
            SetFolderCombo(issue.Folder);
            CmbIssueFolder.IsEnabled = true;
            BtnCreateFolderForIssue.IsEnabled = true;

            LoadSnapshot(ImgSnapshot, issue.Viewpoint?.SnapshotFilePath);
            LoadSnapshot(ImgSnapshot2, issue.Viewpoint?.SnapshotFilePath2);

            BtnMarkupSnapshot3D.Visibility = string.IsNullOrEmpty(issue.Viewpoint?.SnapshotFilePath) || !File.Exists(issue.Viewpoint?.SnapshotFilePath)
                ? Visibility.Collapsed
                : Visibility.Visible;
            BtnMarkupSnapshot2D.Visibility = string.IsNullOrEmpty(issue.Viewpoint?.SnapshotFilePath2) || !File.Exists(issue.Viewpoint?.SnapshotFilePath2)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void SetStatusCombo(string status)
        {
            _isUpdatingStatusCombo = true;
            try
            {
                CmbIssueStatus.SelectedIndex = -1;
                foreach (ComboBoxItem item in CmbIssueStatus.Items)
                {
                    if (string.Equals(item.Content?.ToString(), status, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbIssueStatus.SelectedItem = item;
                        break;
                    }
                }
            }
            finally
            {
                _isUpdatingStatusCombo = false;
            }
        }

        private void PopulateFolderCombo()
        {
            if (_currentIssues == null) return;
            SyncKnownFoldersFromIssues();
            var folders = _knownFolders.OrderBy(f => f).ToList();
            
            _isUpdatingFolderCombo = true;
            try
            {
                CmbIssueFolder.ItemsSource = folders;
            }
            finally
            {
                _isUpdatingFolderCombo = false;
            }
        }

        private void SetFolderCombo(string folder)
        {
            _isUpdatingFolderCombo = true;
            try
            {
                CmbIssueFolder.Text = folder ?? "";
            }
            finally
            {
                _isUpdatingFolderCombo = false;
            }
        }

        private static void LoadSnapshot(System.Windows.Controls.Image image, string path)
        {
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                image.Source = bitmap;
            }
            else
            {
                image.Source = null;
            }
        }

        private static string FormatClashFileStatus(IssueModel issue)
        {
            ViewpointModel viewpoint = issue?.Viewpoint;
            int loadedSnapshots = 0;
            if (File.Exists(viewpoint?.SnapshotFilePath)) loadedSnapshots++;
            if (File.Exists(viewpoint?.SnapshotFilePath2)) loadedSnapshots++;

            string fileText = "Item files: (not found)";
            if (viewpoint?.ClashModelFiles != null && viewpoint.ClashModelFiles.Count > 0)
            {
                string item1 = viewpoint.ClashModelFiles.Count > 0 ? viewpoint.ClashModelFiles[0] : "(not found)";
                string item2 = viewpoint.ClashModelFiles.Count > 1 ? viewpoint.ClashModelFiles[1] : "(not found)";
                fileText = $"Item 1: {item1} | Item 2: {item2}";
            }

            return $"{fileText} | Snapshot: {loadedSnapshots}/2 loaded";
        }

        private string GetAutoSaveFilePath()
        {
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Antigravity", "IssueManager");
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            return System.IO.Path.Combine(dir, "autosave.bcfzip");
        }

        private void AutoSaveIssues()
        {
            string logFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AntigravityIssueManager_RuntimeLog.txt");
            try
            {
                System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] AutoSaveIssues started. Issues count={_currentIssues?.Count ?? 0}\n");
                
                PrepareSnapshotsForStorage(_currentIssues);
                try
                {
                    string projectIdentifier = GetProjectIdentifier();
                    System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] ProjectIdentifier='{projectIdentifier}'\n");
                    
                    IssueBackupService.SaveBackup(_currentIssues, projectIdentifier);
                    System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] SaveBackup completed successfully. LatestBackupPath='{IssueBackupService.LatestBackupPath}'\n");
                }
                catch (Exception backupEx)
                {
                    System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] SaveBackup failed: {backupEx.ToString()}\n");
                    System.Diagnostics.Debug.WriteLine("Issue backup failed: " + backupEx.Message);
                }

                _saveIssuesHandler.Issues = _currentIssues ?? new List<IssueModel>();
                _saveIssuesEvent.Raise();
                System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] SaveIssuesHandler event raised.\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] AutoSave failed: {ex.ToString()}\n");
                System.Diagnostics.Debug.WriteLine("AutoSave failed: " + ex.Message);
            }
        }

        private string GetProjectIdentifier()
        {
            Autodesk.Revit.DB.Document doc = _uiApp?.ActiveUIDocument?.Document;
            if (doc == null) return string.Empty;
            return string.IsNullOrWhiteSpace(doc.PathName) ? doc.Title : doc.PathName;
        }

        private void AutoLoadIssues()
        {
            string logFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AntigravityIssueManager_AutoLoadLog.txt");
            try
            {
                System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] AutoLoadIssues started.\n");

                Autodesk.Revit.DB.Document doc = _uiApp.ActiveUIDocument?.Document;
                string projectIdentifier = doc == null ? string.Empty : (string.IsNullOrWhiteSpace(doc.PathName) ? doc.Title : doc.PathName);

                _currentIssues = IssueStorageService.LoadIssuesFromDocument(doc);
                if (_currentIssues == null) _currentIssues = new List<IssueModel>();
                RestoreSnapshotsFromStorage(_currentIssues);
                System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] Loaded {_currentIssues.Count} RVT issue(s).\n");

                // 1. Merge the project-specific backup automatically.
                // This catches cases where the model was opened before the external save event wrote to RVT.
                if (!string.IsNullOrWhiteSpace(projectIdentifier))
                {
                    string projectBackupPath = IssueBackupService.GetBackupPath(projectIdentifier);
                    if (System.IO.File.Exists(projectBackupPath))
                    {
                        List<IssueModel> backupIssues = IssueBackupService.LoadLatestBackup(projectIdentifier);
                        int beforeCount = _currentIssues.Count;
                        _currentIssues = MergeIssuesPreferBackup(_currentIssues, backupIssues);
                        RestoreSnapshotsFromStorage(_currentIssues);
                        int restoredCount = Math.Max(0, _currentIssues.Count - beforeCount);
                        System.IO.File.AppendAllText(
                            logFile,
                            $"[{DateTime.Now}] Merged project backup. Total={_currentIssues.Count}, Added={restoredCount}, Path={projectBackupPath}\n");

                        if (restoredCount > 0 || beforeCount == 0)
                        {
                            AutoSaveIssues();
                        }
                    }
                }

                // 2. Fall back to global backup (ask user) if no project-specific backup was found
                if (_currentIssues.Count == 0 && IssueBackupService.HasLatestBackup())
                {
                    string pathToShow = IssueBackupService.LatestBackupPath;
                    MessageBoxResult restore = MessageBox.Show(
                        $"No issues were found in the current RVT file.\n\n" +
                        $"A global backup is available:\n{pathToShow}\n\n" +
                        $"Restore issues from this backup?",
                        "Issue Manager Backup",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (restore == MessageBoxResult.Yes)
                    {
                        _currentIssues = IssueBackupService.LoadLatestBackup();
                        if (_currentIssues == null) _currentIssues = new List<IssueModel>();
                        RestoreSnapshotsFromStorage(_currentIssues);
                        System.IO.File.AppendAllText(
                            logFile,
                            $"[{DateTime.Now}] Restored {_currentIssues.Count} issue(s) from global backup: {pathToShow}\n");
                        AutoSaveIssues(); // This will save it as project-specific now!
                    }
                    else
                    {
                        System.IO.File.AppendAllText(
                            logFile,
                            $"[{DateTime.Now}] Global backup restore skipped by user: {pathToShow}\n");
                    }
                }

                // 2. Fall back to legacy autosave.bcfzip only if still empty
                if (_currentIssues.Count == 0)
                {
                    string path = GetAutoSaveFilePath();
                    System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] Legacy autosave path: {path}\n");

                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] Legacy file exists. Parsing...\n");
                        BcfZipParser parser = new BcfZipParser();
                        _currentIssues = parser.ParseBcfZip(path);
                        if (_currentIssues == null) _currentIssues = new List<IssueModel>();
                        System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] Parsed {_currentIssues.Count} legacy issue(s).\n");
                        if (IsLegacyInvalidAutosave(_currentIssues))
                        {
                            _currentIssues = new List<IssueModel>();
                            try
                            {
                                System.IO.File.Delete(path);
                            }
                            catch
                            {
                            }

                            System.IO.File.AppendAllText(
                                logFile,
                                $"[{DateTime.Now}] Legacy invalid autosave deleted.\n");
                            MessageBox.Show(
                                "Old autosave data does not contain Element ID, item names, clash point, or camera data.\n\n" +
                                "Please click Load XML and select the original Navisworks XML again.",
                                "Issue Manager",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }
                }

                RefreshIssueViews();
                UpdateDashboardSummary();
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] AutoLoad failed: {ex.ToString()}\n");
                System.Diagnostics.Debug.WriteLine("AutoLoad failed: " + ex.Message);
            }
        }

        private void OnSaveIssuesError(Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine("Save issues failed: " + ex.Message);
            });
        }

        private static void PrepareSnapshotsForStorage(List<IssueModel> issues)
        {
            if (issues == null) return;

            foreach (IssueModel issue in issues.ToList())
            {
                ViewpointModel viewpoint = issue?.Viewpoint;
                if (viewpoint == null) continue;

                viewpoint.SnapshotBase64 = ReadSnapshotBase64(viewpoint.SnapshotFilePath, viewpoint.SnapshotBase64);
                viewpoint.SnapshotBase642 = ReadSnapshotBase64(viewpoint.SnapshotFilePath2, viewpoint.SnapshotBase642);
            }
        }

        private static string ReadSnapshotBase64(string path, string existingBase64)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    return Convert.ToBase64String(File.ReadAllBytes(path));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReadSnapshotBase64 failed: " + ex.Message);
            }

            return existingBase64;
        }

        private static void RestoreSnapshotsFromStorage(List<IssueModel> issues)
        {
            if (issues == null) return;

            string dir = Path.Combine(Path.GetTempPath(), "AntigravityIssueManager", "RvtSnapshots");
            Directory.CreateDirectory(dir);

            foreach (IssueModel issue in issues)
            {
                ViewpointModel viewpoint = issue?.Viewpoint;
                if (viewpoint == null) continue;

                viewpoint.SnapshotFilePath = RestoreSnapshotFile(viewpoint.SnapshotBase64, dir, issue?.IssueId, "snapshot1", viewpoint.SnapshotFilePath);
                viewpoint.SnapshotFilePath2 = RestoreSnapshotFile(viewpoint.SnapshotBase642, dir, issue?.IssueId, "snapshot2", viewpoint.SnapshotFilePath2);
            }
        }

        private static string RestoreSnapshotFile(string base64, string dir, string issueId, string suffix, string existingPath)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                return existingPath;
            }

            try
            {
                string safeIssueId = SanitizeFileName(string.IsNullOrWhiteSpace(issueId) ? Guid.NewGuid().ToString() : issueId);
                string path = Path.Combine(dir, $"{safeIssueId}_{suffix}.png");
                File.WriteAllBytes(path, Convert.FromBase64String(base64));
                return path;
            }
            catch
            {
                return existingPath;
            }
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }

        private static bool IsLegacyInvalidAutosave(List<IssueModel> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return false;
            }

            return issues.All(issue =>
            {
                ViewpointModel viewpoint = issue.Viewpoint;
                if (viewpoint == null) return true;

                bool hasElementIds = viewpoint.ElementIds != null && viewpoint.ElementIds.Count > 0;
                bool hasFiles = viewpoint.ClashModelFiles != null && viewpoint.ClashModelFiles.Count > 0;
                bool hasClashPoint = viewpoint.HasClashPoint;
                bool hasCamera =
                    Math.Abs(viewpoint.CameraX) > 0.000001 ||
                    Math.Abs(viewpoint.CameraY) > 0.000001 ||
                    Math.Abs(viewpoint.CameraZ) > 0.000001;
                bool hasDirection =
                    Math.Abs(viewpoint.CameraDirectionX) > 0.000001 ||
                    Math.Abs(viewpoint.CameraDirectionY) > 0.000001 ||
                    Math.Abs(viewpoint.CameraDirectionZ) > 0.000001;

                return !hasElementIds && !hasFiles && !hasClashPoint && !(hasCamera && hasDirection);
            });
        }

        private static List<IssueModel> MergeIssuesPreferBackup(List<IssueModel> currentIssues, List<IssueModel> backupIssues)
        {
            List<IssueModel> merged = currentIssues != null
                ? new List<IssueModel>(currentIssues)
                : new List<IssueModel>();

            if (backupIssues == null || backupIssues.Count == 0)
            {
                return merged;
            }

            Dictionary<string, int> indexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < merged.Count; i++)
            {
                string key = GetIssueMergeKey(merged[i]);
                if (!string.IsNullOrWhiteSpace(key) && !indexByKey.ContainsKey(key))
                {
                    indexByKey[key] = i;
                }
            }

            foreach (IssueModel backupIssue in backupIssues)
            {
                string key = GetIssueMergeKey(backupIssue);
                if (!string.IsNullOrWhiteSpace(key) && indexByKey.TryGetValue(key, out int existingIndex))
                {
                    merged[existingIndex] = backupIssue;
                }
                else
                {
                    merged.Add(backupIssue);
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        indexByKey[key] = merged.Count - 1;
                    }
                }
            }

            return merged;
        }

        private static string GetIssueMergeKey(IssueModel issue)
        {
            if (issue == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(issue.IssueId)) return "id:" + issue.IssueId.Trim();
            if (!string.IsNullOrWhiteSpace(issue.IssueCode)) return "code:" + issue.IssueCode.Trim();

            string title = issue.Title ?? string.Empty;
            string created = issue.CreationDate.ToUniversalTime().Ticks.ToString();
            return "fallback:" + title.Trim() + "|" + created;
        }

        private void OnIssueCreated(IssueModel newIssue)
        {
            Dispatcher.Invoke(() =>
            {
                _currentIssues.Add(newIssue);
                RefreshIssueViews();
                LvIssues.SelectedItem = newIssue;
                SelectIssue(newIssue);

                UpdateDashboardSummary();
                AutoSaveIssues();

                MessageBox.Show("Issue created successfully.", "Create Issue", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void OnCreateIssueError(Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Failed to create issue: {ex.Message}", "Create Issue", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void BtnReloadBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string projectIdentifier = GetProjectIdentifier();
                bool hasProjectBackup = IssueBackupService.HasLatestBackup(projectIdentifier);
                bool hasAnyBackup = IssueBackupService.HasLatestBackup();

                if (!hasProjectBackup && !hasAnyBackup)
                {
                    MessageBox.Show(
                        "No backup file was found in:\n" + IssueBackupService.BackupDirectory,
                        "Reload Backup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                List<IssueModel> backupIssues = IssueBackupService.LoadLatestBackup(projectIdentifier);
                if (backupIssues == null || backupIssues.Count == 0)
                {
                    MessageBox.Show(
                        "Backup file was found, but it does not contain any issues.",
                        "Reload Backup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                int beforeCount = _currentIssues?.Count ?? 0;
                _currentIssues = MergeIssuesPreferBackup(_currentIssues, backupIssues);
                RestoreSnapshotsFromStorage(_currentIssues);
                RefreshIssueViews();
                UpdateDashboardSummary();
                AutoSaveIssues();

                MessageBox.Show(
                    $"Reloaded backup from:\n{IssueBackupService.BackupDirectory}\n\n" +
                    $"Issues before: {beforeCount}\nIssues after: {_currentIssues.Count}",
                    "Reload Backup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to reload backup: {ex.Message}", "Reload Backup", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLoadBcf_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "BCF files (*.bcf;*.bcfzip)|*.bcf;*.bcfzip";
            if (dlg.ShowDialog() == true)
            {
                BcfZipParser parser = new BcfZipParser();
                List<IssueModel> loadedIssues = parser.ParseBcfZip(dlg.FileName);
                if (_currentIssues == null) _currentIssues = new List<IssueModel>();
                if (loadedIssues != null) _currentIssues.AddRange(loadedIssues);
                RefreshIssueViews();
                UpdateDashboardSummary();
                AutoSaveIssues();
            }
        }

        private void BtnLoadXml_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "XML files (*.xml)|*.xml";
            if (dlg.ShowDialog() == true)
            {
                NavisworksXmlParser parser = new NavisworksXmlParser();
                List<IssueModel> loadedIssues = parser.ParseReport(dlg.FileName);
                _lastXmlDiagnosticPath = parser.LastDiagnosticPath;
                if (_currentIssues == null) _currentIssues = new List<IssueModel>();
                if (loadedIssues != null) _currentIssues.AddRange(loadedIssues);
                LogLoadedXmlIssues(dlg.FileName, loadedIssues, _lastXmlDiagnosticPath);
                RefreshIssueViews();
                UpdateDashboardSummary();
                AutoSaveIssues();
                ShowXmlLoadSummary(loadedIssues);
            }
        }

        private static void LogRuntimeAssemblyInfo()
        {
            try
            {
                string logFile = Path.Combine(Path.GetTempPath(), "AntigravityIssueManager_RuntimeLog.txt");
                Assembly assembly = typeof(IssueManagerWindow).Assembly;
                File.AppendAllText(
                    logFile,
                    $"[{DateTime.Now}] Build=2026-06-19.10 Assembly={assembly.Location}{Environment.NewLine}");
            }
            catch
            {
            }
        }

        private static void LogLoadedXmlIssues(string xmlPath, List<IssueModel> issues, string diagnosticPath)
        {
            try
            {
                string logFile = Path.Combine(Path.GetTempPath(), "AntigravityIssueManager_RuntimeLog.txt");
                List<string> lines = new List<string>
                {
                    $"[{DateTime.Now}] Load XML: {xmlPath}",
                    $"Diagnostic: {diagnosticPath}",
                    $"Issues: {issues?.Count ?? 0}"
                };

                if (issues != null)
                {
                    foreach (IssueModel issue in issues)
                    {
                        string ids = issue.Viewpoint?.ElementIds == null
                            ? string.Empty
                            : string.Join(",", issue.Viewpoint.ElementIds);
                        string files = issue.Viewpoint?.ClashModelFiles == null
                            ? string.Empty
                            : string.Join(" <-> ", issue.Viewpoint.ClashModelFiles);
                        lines.Add($"{issue.Title}: ids=[{ids}] files=[{files}]");
                    }
                }

                lines.Add(string.Empty);
                File.AppendAllLines(logFile, lines);
            }
            catch
            {
            }
        }

        private void ShowXmlLoadSummary(List<IssueModel> loadedIssues)
        {
            int issueCount = loadedIssues?.Count ?? 0;
            int snapshotCount = loadedIssues?
                .Count(issue => File.Exists(issue.Viewpoint?.SnapshotFilePath) ||
                                File.Exists(issue.Viewpoint?.SnapshotFilePath2)) ?? 0;
            int itemFileCount = loadedIssues?
                .Count(issue => issue.Viewpoint?.ClashModelFiles != null &&
                                issue.Viewpoint.ClashModelFiles.Count > 0) ?? 0;
            int elementIdCount = loadedIssues?
                .Count(issue => issue.Viewpoint?.ElementIds != null &&
                                issue.Viewpoint.ElementIds.Count > 0) ?? 0;

            if (issueCount > 0 && (snapshotCount == 0 || itemFileCount == 0 || elementIdCount == 0))
            {
                MessageBox.Show(
                    $"XML loaded: {issueCount} issue(s).\n" +
                    $"Item files found: {itemFileCount}/{issueCount}\n" +
                    $"Element IDs found: {elementIdCount}/{issueCount}\n" +
                    $"Snapshots found: {snapshotCount}/{issueCount}\n\n" +
                    "If item files or element IDs are 0, this XML export does not include the data in the fields currently parsed. " +
                    "Diagnostic file:\n" + (_lastXmlDiagnosticPath ?? "(not available)"),
                    "Load XML",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void BtnCreateIssue_Click(object sender, RoutedEventArgs e)
        {
            if (_createIssueDialog != null && _createIssueDialog.IsLoaded)
            {
                _createIssueDialog.Focus();
                return;
            }

            _createIssueDialog = new CreateIssueDialog();
            _createIssueDialog.Owner = this;
            
            _createIssueDialog.OnCapture3DRequested = () =>
            {
                _createIssueHandler.IsCaptureOnly = true;
                _createIssueEvent.Raise();
            };

            _createIssueDialog.OnCreateRequested = (dialog) =>
            {
                _createIssueHandler.IsCaptureOnly = false;
                _createIssueHandler.Title = dialog.IssueTitle;
                _createIssueHandler.Level = dialog.IssueLevel;
                _createIssueHandler.AssignedTo = dialog.IssueAssignedTo;
                _createIssueHandler.Description = dialog.IssueDescription;
                _createIssueHandler.Image2DPath = dialog.Image2DPath;
                _createIssueHandler.Image3DPath = dialog.Image3DPath;
                _createIssueHandler.IncludeSectionBoxClipPlanes = ChkIncludeSectionBox.IsChecked == true;
                _createIssueHandler.UseSharedCoordinates = ChkUseSharedCoordinates.IsChecked == true;
                _createIssueEvent.Raise();
                
                dialog.Close();
            };

            _createIssueDialog.ShowDialog();
        }

        private void BtnExportBcf_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIssues == null || _currentIssues.Count == 0)
            {
                MessageBox.Show("There are no issues to export.", "Export BCF", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            List<IssueModel> issuesToExport = SelectIssuesForExport();
            if (issuesToExport == null || issuesToExport.Count == 0) return;

            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "BCF Zip (*.bcfzip)|*.bcfzip";
            dlg.FileName = $"Issues_Export_{DateTime.Now:yyyyMMdd_HHmm}.bcfzip";
            if (dlg.ShowDialog() != true) return;

            try
            {
                BcfExporter.ExportToBcf(issuesToExport, dlg.FileName);
                MessageBox.Show($"Exported {issuesToExport.Count} issue(s) to BCF.", "Export BCF", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export BCF: {ex.Message}", "Export BCF", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIssues == null || _currentIssues.Count == 0)
            {
                MessageBox.Show("There are no issues to export.", "Export Excel", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            List<IssueModel> issuesToExport = SelectIssuesForExport();
            if (issuesToExport == null || issuesToExport.Count == 0) return;

            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
            dlg.FileName = $"RFI_Register_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            if (dlg.ShowDialog() != true) return;

            try
            {
                PrepareSnapshotsForStorage(issuesToExport);
                ExcelIssueExporter.ExportIssues(issuesToExport, dlg.FileName);
                MessageBox.Show($"Exported {issuesToExport.Count} issue(s) to Excel.", "Export Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export Excel: {ex.Message}", "Export Excel", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<IssueModel> SelectIssuesForExport()
        {
            ExportIssueSelectionDialog dialog = new ExportIssueSelectionDialog(_currentIssues, _selectedIssue);
            dialog.Owner = this;
            if (dialog.ShowDialog() != true)
            {
                return null;
            }

            return dialog.SelectedIssues;
        }

        private IEnumerable<string> GetFallbackModelFileNamesForBcfExport()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Autodesk.Revit.DB.Document doc = _uiApp?.ActiveUIDocument?.Document;
            AddModelFileName(names, doc?.PathName);
            AddModelFileName(names, doc?.Title);

            if (doc != null)
            {
                try
                {
                    Autodesk.Revit.DB.FilteredElementCollector collector =
                        new Autodesk.Revit.DB.FilteredElementCollector(doc)
                            .OfClass(typeof(Autodesk.Revit.DB.RevitLinkInstance));

                    foreach (Autodesk.Revit.DB.RevitLinkInstance link in collector)
                    {
                        Autodesk.Revit.DB.Document linkDoc = link.GetLinkDocument();
                        AddModelFileName(names, linkDoc?.PathName);
                        AddModelFileName(names, linkDoc?.Title);
                    }
                }
                catch
                {
                    // Link metadata is a best-effort fallback for BCF related model names.
                }
            }

            return names;
        }

        private static void AddModelFileName(HashSet<string> names, string value)
        {
            if (names == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string trimmed = value.Trim();
            string fileName = trimmed;
            try
            {
                fileName = Path.GetFileName(trimmed.Replace('/', Path.DirectorySeparatorChar));
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                names.Add(fileName);
            }
        }

        private void LvIssues_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LvIssues.SelectedItem is IssueModel issue)
            {
                SelectIssue(issue);
            }
        }

        private void TvIssueGroups_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is IssueModel issue)
            {
                SelectIssue(issue);
            }
        }

        private void CmbViewMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LvIssues == null || TvIssueGroups == null) return;

            bool groupMode = CmbViewMode.SelectedIndex == 1 || CmbViewMode.SelectedIndex == 2;
            LvIssues.Visibility = groupMode ? Visibility.Collapsed : Visibility.Visible;
            TvIssueGroups.Visibility = groupMode ? Visibility.Visible : Visibility.Collapsed;
            RefreshIssueViews();
        }

        private void CmbIssueStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingStatusCombo || _selectedIssue == null) return;
            if (!(CmbIssueStatus.SelectedItem is ComboBoxItem selectedItem)) return;

            string newStatus = selectedItem.Content?.ToString();
            if (string.IsNullOrWhiteSpace(newStatus)) return;

            _selectedIssue.Status = newStatus;
            UpdateDashboardSummary();
            RefreshIssueViews();
            AutoSaveIssues();
        }

        private void CmbIssueFolder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFolderCombo || _selectedIssue == null) return;
            
            if (CmbIssueFolder.SelectedItem is string selectedFolder)
            {
                UpdateIssueFolder(selectedFolder);
            }
        }

        private void CmbIssueFolder_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingFolderCombo || _selectedIssue == null) return;
            
            string newFolder = CmbIssueFolder.Text?.Trim();
            UpdateIssueFolder(newFolder);
        }

        private void BtnNewFolder_Click(object sender, RoutedEventArgs e)
        {
            CreateFolder(assignToSelectedIssue: false);
        }

        private void BtnCreateFolderForIssue_Click(object sender, RoutedEventArgs e)
        {
            CreateFolder(assignToSelectedIssue: true);
        }

        private void CreateFolder(bool assignToSelectedIssue)
        {
            FolderNameDialog dialog = new FolderNameDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() != true) return;

            string folderName = dialog.FolderName;
            _knownFolders.Add(folderName);

            if (assignToSelectedIssue && _selectedIssue != null)
            {
                UpdateIssueFolder(folderName);
            }
            else
            {
                PopulateFolderCombo();
                RefreshIssueViews();
            }
        }

        private void UpdateIssueFolder(string newFolder)
        {
            if (string.IsNullOrWhiteSpace(newFolder)) newFolder = "Uncategorized";
            _knownFolders.Add(newFolder);
            
            if (_selectedIssue.Folder != newFolder)
            {
                _selectedIssue.Folder = newFolder;
                RefreshIssueViews();
                AutoSaveIssues();
            }
        }

        private void ResolveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (!(((FrameworkElement)sender).DataContext is IssueGroupModel group)) return;

            foreach (IssueModel issue in group.Issues)
            {
                issue.Status = "Resolved";
            }

            UpdateDashboardSummary();
            RefreshIssueViews();
            AutoSaveIssues();
        }

        private void OnClashVerified(IssueModel issue, bool resolved, string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (issue != null)
                {
                    SelectIssue(issue);
                    UpdateDashboardSummary();
                    RefreshIssueViews();
                    AutoSaveIssues();
                }

                MessageBox.Show(message, "Verify Clash", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void OnVerifyClashError(Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Failed to verify clash: {ex.Message}", "Verify Clash", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void BtnShowInModel_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIssue != null)
            {
                if (!CanNavigateIssue(_selectedIssue))
                {
                    MessageBox.Show(
                        "This XML issue does not contain enough Revit-matchable data for Show in Model.\n\n" +
                        "Need at least one Element ID/IFC GUID, a Navisworks clash point, or a reliable camera/target from BCF.\n\n" +
                        "Use the diagnostic file from Load XML to map the correct fields, or export BCF/BCFzip from Navisworks if available.",
                        "Show in Model",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                _showIssueHandler.Viewpoint = _selectedIssue.Viewpoint;
                _showIssueEvent.Raise();
            }
        }

        private static bool CanNavigateIssue(IssueModel issue)
        {
            ViewpointModel viewpoint = issue?.Viewpoint;
            if (viewpoint == null) return false;

            if (viewpoint.ElementIds != null && viewpoint.ElementIds.Count > 0)
            {
                return true;
            }

            if (viewpoint.HasClashPoint)
            {
                return true;
            }

            bool hasCamera =
                Math.Abs(viewpoint.CameraX) > 0.000001 ||
                Math.Abs(viewpoint.CameraY) > 0.000001 ||
                Math.Abs(viewpoint.CameraZ) > 0.000001;
            bool hasDirection =
                Math.Abs(viewpoint.CameraDirectionX) > 0.000001 ||
                Math.Abs(viewpoint.CameraDirectionY) > 0.000001 ||
                Math.Abs(viewpoint.CameraDirectionZ) > 0.000001;

            return hasCamera && hasDirection;
        }

        private void BtnIsolateClash_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIssue != null)
            {
                _isolateClashHandler.Viewpoint = _selectedIssue.Viewpoint;
                _isolateClashEvent.Raise();
            }
        }

        private void BtnModelClashPoint_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIssue != null)
            {
                _modelClashHandler.Viewpoint = _selectedIssue.Viewpoint;
                _modelClashEvent.Raise();
            }
        }

        private void BtnVerifyClash_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIssue != null)
            {
                _verifyClashHandler.Issue = _selectedIssue;
                _verifyClashEvent.Raise();
            }
            else
            {
                MessageBox.Show("Please select an issue to verify.", "Verify Clash", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnDeleteIssue_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIssue != null)
            {
                MessageBoxResult result = MessageBox.Show($"Are you sure you want to delete '{_selectedIssue.Title}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    _currentIssues.Remove(_selectedIssue);
                    _selectedIssue = _currentIssues.FirstOrDefault();
                    RefreshIssueViews();
                    
                    SelectIssue(_selectedIssue);
                    
                    UpdateDashboardSummary();
                    AutoSaveIssues();
                }
            }
            else
            {
                MessageBox.Show("Please select an issue to delete.", "Delete Issue", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnEditIssue_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIssue == null) return;

            _createIssueDialog = new CreateIssueDialog();
            _createIssueDialog.Owner = this;
            _createIssueDialog.LoadIssueData(_selectedIssue);

            _createIssueDialog.OnCapture3DRequested = () =>
            {
                _createIssueHandler.IsCaptureOnly = true;
                _createIssueEvent.Raise();
            };

            _createIssueDialog.OnSaveRequested = (dialog) =>
            {
                _selectedIssue.Title = dialog.IssueTitle;
                _selectedIssue.Level = dialog.IssueLevel;
                _selectedIssue.AssignedTo = dialog.IssueAssignedTo;
                _selectedIssue.Description = dialog.IssueDescription;

                if (_selectedIssue.Viewpoint == null)
                {
                    _selectedIssue.Viewpoint = new ViewpointModel();
                }

                if (!string.IsNullOrEmpty(dialog.Image3DPath))
                {
                    _selectedIssue.Viewpoint.SnapshotFilePath = dialog.Image3DPath;
                }
                if (!string.IsNullOrEmpty(dialog.Image2DPath))
                {
                    _selectedIssue.Viewpoint.SnapshotFilePath2 = dialog.Image2DPath;
                }

                if (ChkUseSharedCoordinates.IsChecked == true)
                {
                    ConvertIssueViewpointToSharedCoordinates(_selectedIssue);
                }

                RefreshIssueViews();
                SelectIssue(_selectedIssue);
                UpdateDashboardSummary();
                AutoSaveIssues();

                dialog.Close();
                MessageBox.Show("Issue updated successfully.", "Edit Issue", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            _createIssueDialog.ShowDialog();
        }

        private void ConvertIssueViewpointToSharedCoordinates(IssueModel issue)
        {
            ViewpointModel viewpoint = issue?.Viewpoint;
            DB.Document doc = _uiApp?.ActiveUIDocument?.Document;
            if (viewpoint == null || doc == null)
            {
                return;
            }

            if (string.Equals(viewpoint.CoordinateMode, "Shared", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DB.ProjectPosition projectPosition = doc.ActiveProjectLocation.GetProjectPosition(DB.XYZ.Zero);
            DB.XYZ camera = ToSharedBcfPoint(
                viewpoint.CameraX,
                viewpoint.CameraY,
                viewpoint.CameraZ,
                projectPosition);
            DB.XYZ direction = ToSharedBcfVector(
                viewpoint.CameraDirectionX,
                viewpoint.CameraDirectionY,
                viewpoint.CameraDirectionZ,
                projectPosition);
            DB.XYZ up = ToSharedBcfVector(
                viewpoint.CameraUpX,
                viewpoint.CameraUpY,
                viewpoint.CameraUpZ,
                projectPosition);

            viewpoint.CameraX = camera.X;
            viewpoint.CameraY = camera.Y;
            viewpoint.CameraZ = camera.Z;
            viewpoint.CameraDirectionX = direction.X;
            viewpoint.CameraDirectionY = direction.Y;
            viewpoint.CameraDirectionZ = direction.Z;
            viewpoint.CameraUpX = up.X;
            viewpoint.CameraUpY = up.Y;
            viewpoint.CameraUpZ = up.Z;

            if (viewpoint.HasClashPoint)
            {
                DB.XYZ clashPoint = ToSharedBcfPoint(
                    viewpoint.ClashPointX,
                    viewpoint.ClashPointY,
                    viewpoint.ClashPointZ,
                    projectPosition);
                viewpoint.ClashPointX = clashPoint.X;
                viewpoint.ClashPointY = clashPoint.Y;
                viewpoint.ClashPointZ = clashPoint.Z;
            }

            if (viewpoint.ClippingPlanes != null)
            {
                foreach (ClippingPlaneModel plane in viewpoint.ClippingPlanes)
                {
                    DB.XYZ location = ToSharedBcfPoint(
                        plane.LocationX,
                        plane.LocationY,
                        plane.LocationZ,
                        projectPosition);
                    DB.XYZ planeDirection = ToSharedBcfVector(
                        plane.DirectionX,
                        plane.DirectionY,
                        plane.DirectionZ,
                        projectPosition);

                    plane.LocationX = location.X;
                    plane.LocationY = location.Y;
                    plane.LocationZ = location.Z;
                    plane.DirectionX = planeDirection.X;
                    plane.DirectionY = planeDirection.Y;
                    plane.DirectionZ = planeDirection.Z;
                }
            }

            viewpoint.CoordinateMode = "Shared";
        }

        private static DB.XYZ ToSharedBcfPoint(
            double xMeters,
            double yMeters,
            double zMeters,
            DB.ProjectPosition projectPosition)
        {
            DB.XYZ internalFeet = new DB.XYZ(
                DB.UnitUtils.ConvertToInternalUnits(xMeters, DB.UnitTypeId.Meters),
                DB.UnitUtils.ConvertToInternalUnits(yMeters, DB.UnitTypeId.Meters),
                DB.UnitUtils.ConvertToInternalUnits(zMeters, DB.UnitTypeId.Meters));

            DB.Transform sharedRotation = DB.Transform.CreateRotation(DB.XYZ.BasisZ, projectPosition.Angle);
            DB.XYZ sharedFeet = sharedRotation.OfPoint(internalFeet) + new DB.XYZ(
                projectPosition.EastWest,
                projectPosition.NorthSouth,
                projectPosition.Elevation);

            return new DB.XYZ(
                DB.UnitUtils.ConvertFromInternalUnits(sharedFeet.X, DB.UnitTypeId.Meters),
                DB.UnitUtils.ConvertFromInternalUnits(sharedFeet.Y, DB.UnitTypeId.Meters),
                DB.UnitUtils.ConvertFromInternalUnits(sharedFeet.Z, DB.UnitTypeId.Meters));
        }

        private static DB.XYZ ToSharedBcfVector(
            double x,
            double y,
            double z,
            DB.ProjectPosition projectPosition)
        {
            DB.XYZ internalVector = new DB.XYZ(x, y, z);
            if (internalVector.IsZeroLength())
            {
                return DB.XYZ.Zero;
            }

            DB.Transform sharedRotation = DB.Transform.CreateRotation(DB.XYZ.BasisZ, projectPosition.Angle);
            DB.XYZ sharedVector = sharedRotation.OfVector(internalVector);
            return sharedVector.IsZeroLength() ? DB.XYZ.Zero : sharedVector.Normalize();
        }

        private void BtnMarkupSnapshot3D_Click(object sender, RoutedEventArgs e)
        {
            OpenMarkupEditor(_selectedIssue?.Viewpoint?.SnapshotFilePath, isSnapshot3D: true);
        }

        private void BtnMarkupSnapshot2D_Click(object sender, RoutedEventArgs e)
        {
            OpenMarkupEditor(_selectedIssue?.Viewpoint?.SnapshotFilePath2, isSnapshot3D: false);
        }

        private void OpenMarkupEditor(string imagePath, bool isSnapshot3D)
        {
            if (_selectedIssue == null || string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                MessageBox.Show("Không có ảnh để markup.", "Markup Editor",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var editor = new MarkupEditorWindow(imagePath) { Owner = this };
            if (editor.ShowDialog() != true) return;

            if (_selectedIssue.Viewpoint == null)
            {
                _selectedIssue.Viewpoint = new ViewpointModel();
            }

            if (isSnapshot3D)
                _selectedIssue.Viewpoint.SnapshotFilePath = editor.ResultImagePath;
            else
                _selectedIssue.Viewpoint.SnapshotFilePath2 = editor.ResultImagePath;

            // Reload preview
            LoadSnapshot(isSnapshot3D ? ImgSnapshot : ImgSnapshot2, editor.ResultImagePath);
            AutoSaveIssues();
        }
    }
}
