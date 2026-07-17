using System;
using System.IO;
using System.IO.Compression;

class Program {
    static void Main() {
        try {
            string zip = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Antigravity", "IssueManager", "autosave.bcfzip");
            string temp = Path.Combine(Path.GetTempPath(), "AntigravityBcf", Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp);
            ZipFile.ExtractToDirectory(zip, temp);
            Console.WriteLine("Success!");
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
