using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Antigravity.Setup
{
    internal static class Program
    {
        private static readonly string[] Modules =
        {
            "Antigravity.Core.dll",
            "Antigravity.Main.dll",
            "Antigravity.DrawColumns.dll",
            "Antigravity.DrawBeams.dll",
            "Antigravity.DrawWalls.dll",
            "Antigravity.DrawFloors.dll",
            "Antigravity.Autojoin.dll"
        };

        private const string AddinId = "88888888-9999-0000-AAAA-BBBBCCCCDDDD";

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "Antigravity Revit Add-in Setup";

            try
            {
                bool quiet = args.Any(a => EqualsArg(a, "/quiet") || EqualsArg(a, "--quiet"));
                bool uninstall = args.Any(a => EqualsArg(a, "/uninstall") || EqualsArg(a, "--uninstall"));

                PrintHeader(uninstall);

                string addinsBase = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Autodesk",
                    "Revit",
                    "Addins");

                if (!Directory.Exists(addinsBase))
                {
                    Fail("Không tìm thấy thư mục Revit Addins: " + addinsBase);
                    return 1;
                }

                List<string> versions = Directory.GetDirectories(addinsBase)
                    .Select(Path.GetFileName)
                    .Where(IsVersionFolder)
                    .OrderBy(v => v)
                    .ToList();

                if (versions.Count == 0)
                {
                    Fail("Không phát hiện Revit version nào. Hãy mở Revit ít nhất một lần rồi chạy lại setup.");
                    return 1;
                }

                Console.WriteLine("Phát hiện Revit: " + string.Join(", ", versions.Select(v => "Revit " + v)));
                Console.WriteLine();

                foreach (string version in versions)
                {
                    if (uninstall)
                    {
                        Uninstall(addinsBase, version);
                    }
                    else
                    {
                        Install(addinsBase, version);
                    }
                }

                Console.WriteLine();
                Console.WriteLine(uninstall ? "Hoàn tất gỡ cài đặt." : "Hoàn tất cài đặt.");
                Console.WriteLine("Mở Revit và chọn Always Load nếu Revit hỏi xác nhận add-in.");

                if (!quiet)
                {
                    Console.WriteLine();
                    Console.Write("Nhấn Enter để đóng...");
                    Console.ReadLine();
                }

                return 0;
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
                Console.WriteLine(ex);
                Console.WriteLine();
                Console.Write("Nhấn Enter để đóng...");
                Console.ReadLine();
                return 1;
            }
        }

        private static void Install(string addinsBase, string version)
        {
            string addinFolder = Path.Combine(addinsBase, version);
            string targetFolder = Path.Combine(addinFolder, "Antigravity");
            Directory.CreateDirectory(targetFolder);

            Console.WriteLine("Cài đặt cho Revit " + version);

            foreach (string module in Modules)
            {
                string targetPath = Path.Combine(targetFolder, module);
                ExtractResource(module, targetPath);
                Console.WriteLine("  [OK] " + module);
            }

            string mainDll = Path.Combine(targetFolder, "Antigravity.Main.dll");
            string addinPath = Path.Combine(addinFolder, "Antigravity.addin");
            File.WriteAllText(addinPath, CreateAddinXml(mainDll), new UTF8Encoding(false));
            Console.WriteLine("  [OK] Antigravity.addin");
        }

        private static void Uninstall(string addinsBase, string version)
        {
            string addinFolder = Path.Combine(addinsBase, version);
            string targetFolder = Path.Combine(addinFolder, "Antigravity");
            string addinPath = Path.Combine(addinFolder, "Antigravity.addin");

            Console.WriteLine("Gỡ cài đặt cho Revit " + version);

            if (File.Exists(addinPath))
            {
                File.Delete(addinPath);
                Console.WriteLine("  [OK] Xóa Antigravity.addin");
            }

            if (Directory.Exists(targetFolder))
            {
                Directory.Delete(targetFolder, true);
                Console.WriteLine("  [OK] Xóa thư mục Antigravity");
            }
        }

        private static void ExtractResource(string fileName, string targetPath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                throw new InvalidOperationException("Installer thiếu resource: " + fileName);
            }

            using (Stream input = assembly.GetManifestResourceStream(resourceName))
            using (FileStream output = File.Create(targetPath))
            {
                if (input == null)
                {
                    throw new InvalidOperationException("Không đọc được resource: " + fileName);
                }

                input.CopyTo(output);
            }
        }

        private static string CreateAddinXml(string mainDllPath)
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<RevitAddIns>
  <AddIn Type=""Application"">
    <Name>Antigravity</Name>
    <Assembly>" + EscapeXml(mainDllPath) + @"</Assembly>
    <AddInId>" + AddinId + @"</AddInId>
    <FullClassName>Antigravity.Main.App</FullClassName>
    <VendorId>ANTIGRAVITY</VendorId>
    <VendorDescription>Antigravity Structural Tools</VendorDescription>
  </AddIn>
</RevitAddIns>
";
        }

        private static bool IsVersionFolder(string name)
        {
            if (name == null || name.Length != 4)
            {
                return false;
            }

            int year;
            return int.TryParse(name, out year) && year >= 2022 && year <= 2026;
        }

        private static string EscapeXml(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private static bool EqualsArg(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static void PrintHeader(bool uninstall)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine(uninstall ? " ANTIGRAVITY REVIT ADD-IN UNINSTALLER" : " ANTIGRAVITY REVIT ADD-IN INSTALLER");
            Console.WriteLine("==============================================");
            Console.WriteLine();
        }

        private static void Fail(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[LỖI] " + message);
            Console.ResetColor();
        }
    }
}
