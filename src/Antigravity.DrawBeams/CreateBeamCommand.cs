using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.DrawBeams.UI;
using System;
using System.IO;
using System.Reflection;

namespace Antigravity.DrawBeams
{
    [Transaction(TransactionMode.Manual)]
    public class CreateBeamCommand : IExternalCommand
    {
        static CreateBeamCommand()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAntigravityAssembly;
        }

        private static Assembly ResolveAntigravityAssembly(object sender, ResolveEventArgs args)
        {
            string assemblyName = new AssemblyName(args.Name).Name;
            if (!assemblyName.StartsWith("Antigravity.", StringComparison.OrdinalIgnoreCase))
                return null;

            string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string candidate = Path.Combine(baseDir, assemblyName + ".dll");
            if (File.Exists(candidate))
                return Assembly.LoadFrom(candidate);

            string addinDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "Revit", "Addins", "2024", "Antigravity");
            candidate = Path.Combine(addinDir, assemblyName + ".dll");
            if (File.Exists(candidate))
                return Assembly.LoadFrom(candidate);

            return null;
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            MainWindow window = new MainWindow(uiDoc);
            
            // Re-use current Revit window handle to make UI modal
            IntPtr revitHandle = commandData.Application.MainWindowHandle;
            var interopHelper = new System.Windows.Interop.WindowInteropHelper(window);
            interopHelper.Owner = revitHandle;

            window.Show();

            return Result.Succeeded;
        }
    }
}
