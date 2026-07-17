using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Antigravity.IssueManager.Installer
{
    class Program
    {
        private static readonly string[] SupportedRevitVersions = { "2022", "2023", "2024", "2025", "2026", "2027" };

        static void Main(string[] args)
        {
            Console.Title = "BIM Issue Manager Installer for Revit";
            
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=================================================================");
            Console.WriteLine("                VILAI VIET - ANTINGRAVITY WORKFLOW               ");
            Console.WriteLine("          BIM ISSUE MANAGER INSTALLER FOR AUTODESK REVIT         ");
            Console.WriteLine("=================================================================");
            Console.ResetColor();
            Console.WriteLine();

            try
            {
                // 1. Determine Revit Addins directories.
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string addinsRoot = Path.Combine(appData, @"Autodesk\Revit\Addins");
                string requestedVersion = GetVersionArgument(args);
                string[] versions = GetTargetVersions(addinsRoot, requestedVersion);

                Console.WriteLine($"[*] Revit Addins root: {addinsRoot}");
                Console.WriteLine($"[*] Target version(s): {string.Join(", ", versions)}");

                foreach (string version in versions)
                {
                    string revitAddinsPath = Path.Combine(addinsRoot, version);

                    Console.WriteLine();
                    Console.WriteLine($"[*] Installing for Revit {version}: {revitAddinsPath}");
                    if (!Directory.Exists(revitAddinsPath))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("    Addins directory not found. Creating it...");
                        Console.ResetColor();
                        Directory.CreateDirectory(revitAddinsPath);
                    }

                    // 2. Create Target Folder for DLLs
                    string targetFolder = Path.Combine(revitAddinsPath, "Antigravity.IssueManager");
                    if (!Directory.Exists(targetFolder))
                    {
                        Console.WriteLine("    Creating target installation directory...");
                        Directory.CreateDirectory(targetFolder);
                    }
                    else
                    {
                        Console.WriteLine("    Target folder already exists. Overwriting existing files...");
                    }

                    // 3. Extract DLLs
                    Assembly currentAssembly = Assembly.GetExecutingAssembly();

                    Console.WriteLine("    Extracting add-in components...");
                    string issueManagerDll = Path.Combine(targetFolder, "Antigravity.IssueManager.dll");
                    ExtractResource(currentAssembly, "Antigravity.IssueManager.dll", issueManagerDll);
                    ExtractResource(currentAssembly, "Antigravity.Core.dll", Path.Combine(targetFolder, "Antigravity.Core.dll"));
                    ExtractResource(currentAssembly, "Serilog.dll", Path.Combine(targetFolder, "Serilog.dll"));
                    ExtractResource(currentAssembly, "Serilog.Sinks.File.dll", Path.Combine(targetFolder, "Serilog.Sinks.File.dll"));

                    // 4. Create .addin manifest file
                    Console.WriteLine("    Creating Revit manifest file (.addin)...");
                    string addinFile = Path.Combine(revitAddinsPath, "Antigravity.IssueManager.addin");
                    string addinContent =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<RevitAddIns>
  <AddIn Type=""Application"">
    <Name>BIM Issue Manager</Name>
    <Assembly>Antigravity.IssueManager\Antigravity.IssueManager.dll</Assembly>
    <FullClassName>Antigravity.IssueManager.App</FullClassName>
    <AddInId>11111111-2222-3333-4444-555566667777</AddInId>
    <VendorId>VILAIVIET</VendorId>
    <VendorDescription>Vilai Viet BIM Issue Manager Tools</VendorDescription>
  </AddIn>
</RevitAddIns>";

                    File.WriteAllText(addinFile, addinContent, System.Text.Encoding.UTF8);
                    Console.WriteLine($"    DLL:    {issueManagerDll}");
                    Console.WriteLine($"    Addin:  {addinFile}");
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("=================================================================");
                Console.WriteLine("     SUCCESS: BIM Issue Manager has been installed successfully!  ");
                Console.WriteLine("     Please restart Autodesk Revit to load the add-in.            ");
                Console.WriteLine("=================================================================");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("=================================================================");
                Console.WriteLine("     ERROR: Installation failed!                                 ");
                Console.WriteLine($"     {ex.Message}");
                Console.WriteLine("=================================================================");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static string GetVersionArgument(string[] args)
        {
            if (args == null) return null;
            foreach (string arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg)) continue;
                string value = arg.Trim();
                if (value.StartsWith("/version:", StringComparison.OrdinalIgnoreCase))
                {
                    return value.Substring("/version:".Length);
                }

                if (value.StartsWith("-version:", StringComparison.OrdinalIgnoreCase))
                {
                    return value.Substring("-version:".Length);
                }
            }

            return null;
        }

        private static string[] GetTargetVersions(string addinsRoot, string requestedVersion)
        {
            if (!string.IsNullOrWhiteSpace(requestedVersion))
            {
                return new[] { requestedVersion.Trim() };
            }

            if (Directory.Exists(addinsRoot))
            {
                var existingVersions = SupportedRevitVersions
                    .Where(version => Directory.Exists(Path.Combine(addinsRoot, version)))
                    .ToArray();
                if (existingVersions.Length > 0)
                {
                    return existingVersions;
                }
            }

            return new[] { "2024" };
        }

        private static void ExtractResource(Assembly assembly, string resourceNamePart, string destPath)
        {
            string[] resourceNames = assembly.GetManifestResourceNames();
            string actualResourceName = null;
            foreach (var name in resourceNames)
            {
                if (name.EndsWith(resourceNamePart, StringComparison.OrdinalIgnoreCase))
                {
                    actualResourceName = name;
                    break;
                }
            }

            if (actualResourceName == null)
            {
                throw new Exception($"Cannot find embedded resource: '{resourceNamePart}'");
            }

            using (Stream stream = assembly.GetManifestResourceStream(actualResourceName))
            {
                if (stream == null) 
                    throw new Exception($"Cannot open stream for embedded resource: '{actualResourceName}'");
                
                using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fs);
                }
            }
            
            Console.WriteLine($"    -> Deployed: {Path.GetFileName(destPath)}");
        }
    }
}
