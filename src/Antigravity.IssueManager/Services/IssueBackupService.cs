using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antigravity.IssueManager.Models;

namespace Antigravity.IssueManager.Services
{
    public static class IssueBackupService
    {
        private const int MaxTimestampBackups = 30;
        private const string LatestFileName = "issues_autobackup_latest.json";

        public static string BackupDirectory
        {
            get
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "AntigravityIssueManager", "Backups");
            }
        }

        public static string LatestBackupPath => Path.Combine(BackupDirectory, LatestFileName);

        public static string GetBackupPath(string projectIdentifier)
        {
            if (string.IsNullOrWhiteSpace(projectIdentifier))
            {
                return LatestBackupPath;
            }
            
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(projectIdentifier));
                string hashStr = BitConverter.ToString(hash).Replace("-", "").ToLower().Substring(0, 16);
                return Path.Combine(BackupDirectory, $"issues_autobackup_{hashStr}.json");
            }
        }

        public static void SaveBackup(List<IssueModel> issues, string projectIdentifier = null)
        {
            if (issues == null || issues.Count == 0)
            {
                return;
            }

            Directory.CreateDirectory(BackupDirectory);
            string json = IssueStorageService.SerializeIssues(issues);
            
            // Save to global latest
            File.WriteAllText(LatestBackupPath, json);

            // Save to project-specific latest
            if (!string.IsNullOrWhiteSpace(projectIdentifier))
            {
                File.WriteAllText(GetBackupPath(projectIdentifier), json);
            }

            string timestampPath = Path.Combine(
                BackupDirectory,
                "issues_autobackup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            File.WriteAllText(timestampPath, json);
            PruneOldBackups();
        }

        public static List<IssueModel> LoadLatestBackup(string projectIdentifier = null)
        {
            string path = GetBackupPath(projectIdentifier);
            if (!File.Exists(path))
            {
                path = LatestBackupPath;
            }

            if (!File.Exists(path))
            {
                return new List<IssueModel>();
            }

            string json = File.ReadAllText(path);
            return IssueStorageService.DeserializeIssues(json);
        }

        public static bool HasLatestBackup(string projectIdentifier = null)
        {
            if (!string.IsNullOrWhiteSpace(projectIdentifier) && File.Exists(GetBackupPath(projectIdentifier)))
            {
                return true;
            }
            return File.Exists(LatestBackupPath);
        }

        private static void PruneOldBackups()
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(BackupDirectory);
                if (!dir.Exists) return;

                FileInfo[] timestampBackups = dir
                    .GetFiles("issues_autobackup_*.json")
                    .Where(file => !string.Equals(file.Name, LatestFileName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ToArray();

                foreach (FileInfo oldBackup in timestampBackups.Skip(MaxTimestampBackups))
                {
                    oldBackup.Delete();
                }
            }
            catch
            {
                // Backup pruning must never block issue saving.
            }
        }
    }
}
