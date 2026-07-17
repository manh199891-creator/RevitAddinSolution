using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.IssueManager.Models;

namespace Antigravity.IssueManager.Handlers
{
    public class VerifyClashHandler : IExternalEventHandler
    {
        private const double VolumeTolerance = 0.0001;

        public IssueModel Issue { get; set; }
        public Action<IssueModel, bool, string> OnVerified { get; set; }
        public Action<Exception> OnError { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                if (Issue?.Viewpoint?.ElementIds == null || Issue.Viewpoint.ElementIds.Count < 2)
                {
                    OnVerified?.Invoke(Issue, false, "Issue does not contain two Revit element ids.");
                    return;
                }

                UIDocument uidoc = app.ActiveUIDocument;
                Document doc = uidoc?.Document;
                if (doc == null)
                {
                    OnVerified?.Invoke(Issue, false, "No active Revit document.");
                    return;
                }

                List<Element> elements = Issue.Viewpoint.ElementIds
                    .Select(id => TryGetElement(doc, id))
                    .Where(e => e != null)
                    .Take(2)
                    .ToList();

                if (elements.Count < 2)
                {
                    OnVerified?.Invoke(Issue, false, "Could not find two host elements in the active model.");
                    return;
                }

                List<Solid> solids1 = GetSolids(elements[0]).ToList();
                List<Solid> solids2 = GetSolids(elements[1]).ToList();
                if (solids1.Count == 0 || solids2.Count == 0)
                {
                    OnVerified?.Invoke(Issue, false, "One or both elements do not expose solid geometry.");
                    return;
                }

                bool stillClashing = HasIntersection(solids1, solids2);
                if (!stillClashing)
                {
                    Issue.Status = "Resolved";
                }

                string message = stillClashing
                    ? "Clash still exists."
                    : "No intersection found. Issue marked as Resolved.";
                OnVerified?.Invoke(Issue, !stillClashing, message);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        public string GetName()
        {
            return "Verify Clash Handler";
        }

        private static Element TryGetElement(Document doc, string idText)
        {
            if (string.IsNullOrWhiteSpace(idText)) return null;
            if (!long.TryParse(idText.Trim(), out long idValue)) return null;
            return doc.GetElement(new ElementId(idValue));
        }

        private static IEnumerable<Solid> GetSolids(Element element)
        {
            Options options = new Options
            {
                ComputeReferences = false,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geometry = element.get_Geometry(options);
            if (geometry == null) yield break;

            foreach (Solid solid in ExtractSolids(geometry))
            {
                yield return solid;
            }
        }

        private static IEnumerable<Solid> ExtractSolids(GeometryElement geometry)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid && solid.Volume > VolumeTolerance && solid.Faces.Size > 0)
                {
                    yield return solid;
                }
                else if (obj is GeometryInstance instance)
                {
                    GeometryElement instanceGeometry = instance.GetInstanceGeometry();
                    if (instanceGeometry == null) continue;

                    foreach (Solid instanceSolid in ExtractSolids(instanceGeometry))
                    {
                        yield return instanceSolid;
                    }
                }
            }
        }

        private static bool HasIntersection(IEnumerable<Solid> firstSolids, IEnumerable<Solid> secondSolids)
        {
            foreach (Solid first in firstSolids)
            {
                foreach (Solid second in secondSolids)
                {
                    try
                    {
                        Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                            first,
                            second,
                            BooleanOperationsType.Intersect);

                        if (intersection != null && intersection.Volume > VolumeTolerance)
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // Some Revit solids cannot participate in boolean ops. Try the next pair.
                    }
                }
            }

            return false;
        }
    }
}
