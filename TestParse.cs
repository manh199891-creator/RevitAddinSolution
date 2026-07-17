using System;
using System.Collections.Generic;
using Antigravity.IssueManager.Models;
using Antigravity.IssueManager.Services;
using System.IO;

class Program {
    static void Main() {
        try {
            string zip = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Antigravity", "IssueManager", "autosave.bcfzip");
            BcfZipParser parser = new BcfZipParser();
            var issues = parser.ParseBcfZip(zip);
            Console.WriteLine("Loaded " + issues.Count + " issues.");
            foreach (var issue in issues) {
                Console.WriteLine(issue.DisplayTitle + " (Status: " + issue.Status + ")");
            }
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
