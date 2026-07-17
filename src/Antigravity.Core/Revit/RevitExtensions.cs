using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Antigravity.Core.Revit
{
    public static class RevitExtensions
    {
        /// <summary>
        /// Shortcut to start a transaction
        /// </summary>
        public static Transaction CreateTransaction(this Document doc, string name)
        {
            return new Transaction(doc, name);
        }

        /// <summary>
        /// Simple message box for Revit
        /// </summary>
        public static void ShowMessage(this string message, string title = "Antigravity")
        {
            TaskDialog.Show(title, message);
        }
    }
}
