using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.Autojoin.Models;

namespace Antigravity.Autojoin.Services
{
    public class AutoJoinIntegrationTestResult
    {
        public bool Passed { get; set; }
        public string Message { get; set; }
        public string StatusMessage { get; set; }
        public JoinResult JoinResult { get; set; }
    }

    public static class AutoJoinIntegrationTestService
    {
        public static AutoJoinIntegrationTestResult RunSelectionJoinTest(UIDocument uidoc)
        {
            if (uidoc == null)
                return Fail("No active Revit document.");

            var doc = uidoc.Document;
            var selected = uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .Where(e => e != null && e.IsValidObject && e.Category != null)
                .ToList();

            var beam = selected.FirstOrDefault(e => IsCategory(e, BuiltInCategory.OST_StructuralFraming));
            var floor = selected.FirstOrDefault(e => IsCategory(e, BuiltInCategory.OST_Floors));

            if (beam == null || floor == null)
                return Fail("Select one beam and one floor before running AutoJoin integration test.");

            var rules = new List<JoinRule>
            {
                new JoinRule
                {
                    Order = 1,
                    CategoryA = "OST_StructuralFraming",
                    CategoryB = "OST_Floors",
                    IsEnabled = true
                }
            };

            var result = AutoJoinService.ExecuteJoin(doc, uidoc, rules, JoinScope.Selection);
            var status = JoinStatusFormatter.Format(JoinAction.Join, result);
            var areJoined = JoinGeometryUtils.AreElementsJoined(doc, beam, floor);

            return new AutoJoinIntegrationTestResult
            {
                Passed = areJoined,
                Message = areJoined
                    ? "AutoJoin integration test passed: selected beam and floor are joined."
                    : "AutoJoin integration test failed: selected beam and floor are not joined.",
                StatusMessage = status,
                JoinResult = result
            };
        }

        private static AutoJoinIntegrationTestResult Fail(string message)
        {
            return new AutoJoinIntegrationTestResult
            {
                Passed = false,
                Message = message,
                StatusMessage = "No join result.",
                JoinResult = new JoinResult()
            };
        }

        private static bool IsCategory(Element element, BuiltInCategory category)
        {
            return element.Category.Id.Value == (long)category;
        }
    }
}
