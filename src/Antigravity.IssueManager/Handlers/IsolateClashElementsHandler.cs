using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.IssueManager.Models;

namespace Antigravity.IssueManager.Handlers
{
    public class IsolateClashElementsHandler : IExternalEventHandler
    {
        public ViewpointModel Viewpoint { get; set; }

        public void Execute(UIApplication app)
        {
            if (Viewpoint == null || Viewpoint.ElementIds == null || Viewpoint.ElementIds.Count == 0) return;

            UIDocument uidoc = app.ActiveUIDocument;
            if (uidoc == null) return;
            Document doc = uidoc.Document;

            try
            {
                if (doc.ActiveView is View3D view3d)
                {
                    List<MatchedClashElement> matchedElements = new List<MatchedClashElement>();
                    foreach (string idStr in Viewpoint.ElementIds)
                    {
                        MatchedClashElement match = FindElement(doc, idStr);
                        if (match != null)
                        {
                            AddMatchedElement(matchedElements, match);
                        }
                    }

                    List<ElementId> idsToIsolate = GetVisibilityElementIds(matchedElements);
                    if (idsToIsolate.Count > 0)
                    {
                        using (Transaction tx = new Transaction(doc, "Isolate Clash Elements"))
                        {
                            tx.Start();
                            
                            // Host elements can be isolated directly. Linked elements isolate their link instance.
                            view3d.IsolateElementsTemporary(idsToIsolate);
                            
                            BoundingBoxXYZ combinedBBox = CreateSectionBox(matchedElements);
                            
                            if (combinedBBox != null)
                            {
                                view3d.SetSectionBox(combinedBBox);
                                view3d.IsSectionBoxActive = true;
                            }

                            tx.Commit();
                        }
                        
                        uidoc.Selection.SetElementIds(idsToIsolate);
                        
                        // Zoom to fit
                        UIView uiview = uidoc.GetOpenUIViews().FirstOrDefault(q => q.ViewId == view3d.Id);
                        if (uiview != null)
                        {
                            uiview.ZoomToFit();
                        }
                    }
                    else
                    {
                        TaskDialog.Show("Isolate Elements", "Could not find host or linked elements matching the Element IDs in the clash report.");
                    }
                }
                else
                {
                    TaskDialog.Show("Isolate Elements", "Please open a 3D view to isolate elements.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IsolateClashElementsHandler Error: {ex.Message}");
            }
        }

        public string GetName()
        {
            return "Isolate Clash Elements Handler";
        }

        private static MatchedClashElement FindElement(Document hostDoc, string identifier)
        {
            Element hostElement = FindElementInDoc(hostDoc, identifier);
            if (hostElement != null)
            {
                return new MatchedClashElement(hostElement, null);
            }

            FilteredElementCollector linkCollector = new FilteredElementCollector(hostDoc).OfClass(typeof(RevitLinkInstance));
            foreach (RevitLinkInstance linkInstance in linkCollector)
            {
                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null)
                {
                    continue;
                }

                Element linkElement = FindElementInDoc(linkDoc, identifier);
                if (linkElement != null)
                {
                    return new MatchedClashElement(linkElement, linkInstance);
                }
            }

            return null;
        }

        private static Element FindElementInDoc(Document doc, string identifier)
        {
            if (doc == null || string.IsNullOrWhiteSpace(identifier)) return null;

            string value = identifier.Trim();
            if (long.TryParse(value, out long idLong))
            {
                Element byId = doc.GetElement(new ElementId(idLong));
                if (byId != null)
                {
                    return byId;
                }
            }

            try
            {
                return doc.GetElement(value);
            }
            catch
            {
                return null;
            }
        }

        private static void AddMatchedElement(List<MatchedClashElement> matches, MatchedClashElement match)
        {
            if (matches == null || match == null || match.Element == null) return;

            foreach (MatchedClashElement existing in matches)
            {
                if (ElementIdsEqual(existing.Element.Id, match.Element.Id) &&
                    ElementIdsEqual(existing.LinkInstance?.Id, match.LinkInstance?.Id))
                {
                    return;
                }
            }

            matches.Add(match);
        }

        private static List<ElementId> GetVisibilityElementIds(IEnumerable<MatchedClashElement> matches)
        {
            List<ElementId> ids = new List<ElementId>();
            foreach (MatchedClashElement match in matches)
            {
                AddElementId(ids, match.IsLinked ? match.LinkInstance.Id : match.Element.Id);
            }

            return ids;
        }

        private static void AddElementId(List<ElementId> ids, ElementId id)
        {
            if (ids == null || id == null || ElementIdsEqual(id, ElementId.InvalidElementId)) return;
            if (ids.Any(existing => ElementIdsEqual(existing, id))) return;
            ids.Add(id);
        }

        private static BoundingBoxXYZ CreateSectionBox(IEnumerable<MatchedClashElement> matches)
        {
            bool hasBox = false;
            double minX = 0;
            double minY = 0;
            double minZ = 0;
            double maxX = 0;
            double maxY = 0;
            double maxZ = 0;

            foreach (MatchedClashElement match in matches)
            {
                BoundingBoxXYZ bbox = match.GetTransformedBoundingBox();
                if (bbox == null) continue;

                if (!hasBox)
                {
                    minX = bbox.Min.X;
                    minY = bbox.Min.Y;
                    minZ = bbox.Min.Z;
                    maxX = bbox.Max.X;
                    maxY = bbox.Max.Y;
                    maxZ = bbox.Max.Z;
                    hasBox = true;
                    continue;
                }

                minX = Math.Min(minX, bbox.Min.X);
                minY = Math.Min(minY, bbox.Min.Y);
                minZ = Math.Min(minZ, bbox.Min.Z);
                maxX = Math.Max(maxX, bbox.Max.X);
                maxY = Math.Max(maxY, bbox.Max.Y);
                maxZ = Math.Max(maxZ, bbox.Max.Z);
            }

            if (!hasBox) return null;

            const double paddingFeet = 2.0;
            return new BoundingBoxXYZ
            {
                Transform = Transform.Identity,
                Min = new XYZ(minX - paddingFeet, minY - paddingFeet, minZ - paddingFeet),
                Max = new XYZ(maxX + paddingFeet, maxY + paddingFeet, maxZ + paddingFeet)
            };
        }

        private static bool ElementIdsEqual(ElementId first, ElementId second)
        {
            if (first == null || second == null) return first == null && second == null;
            return first.Equals(second);
        }

        private class MatchedClashElement
        {
            public MatchedClashElement(Element element, RevitLinkInstance linkInstance)
            {
                Element = element;
                LinkInstance = linkInstance;
            }

            public Element Element { get; }
            public RevitLinkInstance LinkInstance { get; }
            public bool IsLinked => LinkInstance != null;

            public BoundingBoxXYZ GetTransformedBoundingBox()
            {
                BoundingBoxXYZ box = Element?.get_BoundingBox(null);
                if (box == null) return null;

                Transform boxTransform = box.Transform ?? Transform.Identity;
                Transform linkTransform = IsLinked ? LinkInstance.GetTotalTransform() : Transform.Identity;

                XYZ[] corners =
                {
                    new XYZ(box.Min.X, box.Min.Y, box.Min.Z),
                    new XYZ(box.Min.X, box.Min.Y, box.Max.Z),
                    new XYZ(box.Min.X, box.Max.Y, box.Min.Z),
                    new XYZ(box.Min.X, box.Max.Y, box.Max.Z),
                    new XYZ(box.Max.X, box.Min.Y, box.Min.Z),
                    new XYZ(box.Max.X, box.Min.Y, box.Max.Z),
                    new XYZ(box.Max.X, box.Max.Y, box.Min.Z),
                    new XYZ(box.Max.X, box.Max.Y, box.Max.Z)
                };

                XYZ first = linkTransform.OfPoint(boxTransform.OfPoint(corners[0]));
                double minX = first.X;
                double minY = first.Y;
                double minZ = first.Z;
                double maxX = first.X;
                double maxY = first.Y;
                double maxZ = first.Z;

                foreach (XYZ corner in corners.Skip(1))
                {
                    XYZ transformed = linkTransform.OfPoint(boxTransform.OfPoint(corner));
                    minX = Math.Min(minX, transformed.X);
                    minY = Math.Min(minY, transformed.Y);
                    minZ = Math.Min(minZ, transformed.Z);
                    maxX = Math.Max(maxX, transformed.X);
                    maxY = Math.Max(maxY, transformed.Y);
                    maxZ = Math.Max(maxZ, transformed.Z);
                }

                return new BoundingBoxXYZ
                {
                    Transform = Transform.Identity,
                    Min = new XYZ(minX, minY, minZ),
                    Max = new XYZ(maxX, maxY, maxZ)
                };
            }
        }
    }
}
