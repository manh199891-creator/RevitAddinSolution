using System;
using System.Collections.Generic;
using Antigravity.IssueManager.Models;
using Antigravity.IssueManager.Services;

class Program {
    static void Main() {
        try {
            var testCases = new List<DateTime> {
                DateTime.Now,
                DateTime.MinValue,
                DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
                DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Local),
                new DateTime(1969, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                new DateTime(1969, 12, 31, 23, 59, 59, DateTimeKind.Local),
                new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Unspecified)
            };

            foreach (var dt in testCases) {
                Console.WriteLine("Testing: Value=" + dt.ToString("yyyy-MM-dd HH:mm:ss") + ", Kind=" + dt.Kind);
                var issues = new List<IssueModel> {
                    new IssueModel {
                        IssueId = Guid.NewGuid().ToString(),
                        Title = "Test Issue",
                        CreationDate = dt
                    }
                };
                string json = IssueStorageService.SerializeIssues(issues);
                Console.WriteLine("  Serialization succeeded!");
                var deserialized = IssueStorageService.DeserializeIssues(json);
                Console.WriteLine("  Deserialization succeeded! Reserialized Kind=" + deserialized[0].CreationDate.Kind);
            }
            Console.WriteLine("All tests passed successfully!");
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.ToString());
        }
    }
}
