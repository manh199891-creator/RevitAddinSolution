using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Antigravity.WallMepClash.Tests
{
    [SetUpFixture]
    public class TestSetup
    {
        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            string revitDir = @"C:\Program Files\Autodesk\Revit 2024";
            
            // Thiết lập Directory hiện tại về thư mục cài đặt Revit để nạp được các native DLL dependencies
            if (Directory.Exists(revitDir))
            {
                Directory.SetCurrentDirectory(revitDir);
                
                // Thêm Revit directory vào PATH để chắc chắn các dependencies được tìm thấy
                string pathEnv = Environment.GetEnvironmentVariable("PATH");
                Environment.SetEnvironmentVariable("PATH", revitDir + ";" + pathEnv);
            }

            // Đăng ký sự kiện AssemblyResolve để tự động tìm kiếm Revit API assemblies trong Revit directory
            AppDomain.CurrentDomain.AssemblyResolve += ResolveRevitAssemblies;
        }

        private Assembly ResolveRevitAssemblies(object sender, ResolveEventArgs args)
        {
            string assemblyName = new AssemblyName(args.Name).Name;
            if (assemblyName.Equals("RevitAPI", StringComparison.OrdinalIgnoreCase) ||
                assemblyName.Equals("RevitAPIUI", StringComparison.OrdinalIgnoreCase))
            {
                string revitDir = @"C:\Program Files\Autodesk\Revit 2024";
                string candidate = Path.Combine(revitDir, assemblyName + ".dll");
                if (File.Exists(candidate))
                {
                    return Assembly.LoadFrom(candidate);
                }
            }
            return null;
        }
    }
}
