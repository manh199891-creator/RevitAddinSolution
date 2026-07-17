using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.AutoDimWalls.Models;

namespace Antigravity.AutoDimWalls.Core
{
    public static class OpeningReferenceResolver
    {
        public static IList<ReferenceInfo> GetOpeningReferences(
            Document doc,
            Wall wall,
            StraightWallInfo wallInfo,
            View activeView,
            OpeningReferenceMode mode,
            List<string> logs)
        {
            var categories = new[]
            {
                BuiltInCategory.OST_Doors,
                BuiltInCategory.OST_Windows
            };

            var filter = new ElementMulticategoryFilter(categories);
            var openings = new FilteredElementCollector(doc)
                .WherePasses(filter)
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>()
                .Where(instance => instance.Host != null && instance.Host.Id == wall.Id)
                .ToList();

            var references = new List<ReferenceInfo>();

            if (mode == OpeningReferenceMode.Center)
            {
                foreach (FamilyInstance opening in openings)
                {
                    XYZ locPoint = null;
                    if (opening.Location is LocationPoint lp)
                    {
                        locPoint = lp.Point;
                    }
                    else if (opening.Location is LocationCurve lc)
                    {
                        locPoint = (lc.Curve.GetEndPoint(0) + lc.Curve.GetEndPoint(1)) * 0.5;
                    }

                    double t_door = locPoint != null ? (locPoint - wallInfo.Start).DotProduct(wallInfo.Direction) : 0.0;

                    var refsTemp = new List<Reference>();
                    AddReferences(opening, FamilyInstanceReferenceType.CenterLeftRight, refsTemp, logs);
                    foreach (Reference r in refsTemp)
                    {
                        references.Add(new ReferenceInfo { Reference = r, Parameter = t_door });
                    }
                }
                return references;
            }

            // For Jambs mode, we extract the actual cut faces from the wall geometry to get the masonry opening (e.g., 950mm).
            var options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true
            };

            GeometryElement geometry = wall.get_Geometry(options);
            var candidates = new List<Tuple<double, Reference>>();
            if (geometry != null)
            {
                WallGeometryUtils.CollectEndFaces(geometry, wallInfo, candidates, logs);
            }

            foreach (FamilyInstance opening in openings)
            {
                // Get location point of opening
                XYZ locPoint = null;
                if (opening.Location is LocationPoint lp)
                {
                    locPoint = lp.Point;
                }
                else if (opening.Location is LocationCurve lc)
                {
                    locPoint = (lc.Curve.GetEndPoint(0) + lc.Curve.GetEndPoint(1)) * 0.5;
                }

                if (locPoint == null)
                    continue;

                // Project opening center onto wall line
                double t_door = (locPoint - wallInfo.Start).DotProduct(wallInfo.Direction);

                // Find candidate jamb faces within 6 feet buffer of the opening center
                var doorCandidates = candidates
                    .Where(c => Math.Abs(c.Item1 - t_door) < 6.0)
                    .ToList();

                // Left jamb: c.Item1 < t_door, pick closest (maximum parameter)
                var leftJambCandidate = doorCandidates
                    .Where(c => c.Item1 < t_door - 0.01)
                    .OrderByDescending(c => c.Item1)
                    .FirstOrDefault();

                // Right jamb: c.Item1 > t_door, pick closest (minimum parameter)
                var rightJambCandidate = doorCandidates
                    .Where(c => c.Item1 > t_door + 0.01)
                    .OrderBy(c => c.Item1)
                    .FirstOrDefault();

                if (leftJambCandidate != null)
                {
                    references.Add(new ReferenceInfo { Reference = leftJambCandidate.Item2, Parameter = leftJambCandidate.Item1 });
                }
                else
                {
                    var leftRefs = new List<Reference>();
                    AddReferences(opening, FamilyInstanceReferenceType.Left, leftRefs, logs);
                    if (leftRefs.Count > 0)
                    {
                        double w = GetOpeningWidth(opening);
                        references.Add(new ReferenceInfo { Reference = leftRefs[0], Parameter = t_door - w * 0.5 });
                        logs?.Add($"Opening {opening.Id.Value}: Left jamb fallback to FamilyInstance Left reference plane.");
                    }
                    else
                    {
                        logs?.Add($"Opening {opening.Id.Value}: Left jamb not found in wall geometry or family references");
                    }
                }

                if (rightJambCandidate != null)
                {
                    references.Add(new ReferenceInfo { Reference = rightJambCandidate.Item2, Parameter = rightJambCandidate.Item1 });
                }
                else
                {
                    var rightRefs = new List<Reference>();
                    AddReferences(opening, FamilyInstanceReferenceType.Right, rightRefs, logs);
                    if (rightRefs.Count > 0)
                    {
                        double w = GetOpeningWidth(opening);
                        references.Add(new ReferenceInfo { Reference = rightRefs[0], Parameter = t_door + w * 0.5 });
                        logs?.Add($"Opening {opening.Id.Value}: Right jamb fallback to FamilyInstance Right reference plane.");
                    }
                    else
                    {
                        logs?.Add($"Opening {opening.Id.Value}: Right jamb not found in wall geometry or family references");
                    }
                }
            }

            return references;
        }

        private static double GetOpeningWidth(FamilyInstance instance)
        {
            double width = 3.0; // default 3 feet (~900mm)
            Parameter p = instance.LookupParameter("Width") 
                       ?? instance.Symbol.LookupParameter("Width")
                       ?? instance.LookupParameter("Độ rộng")
                       ?? instance.Symbol.LookupParameter("Độ rộng")
                       ?? instance.get_Parameter(BuiltInParameter.GENERIC_WIDTH)
                       ?? instance.Symbol.get_Parameter(BuiltInParameter.GENERIC_WIDTH);
            if (p != null)
                width = p.AsDouble();
            return width;
        }

        private static void AddReferences(
            FamilyInstance instance,
            FamilyInstanceReferenceType referenceType,
            ICollection<Reference> references,
            List<string> logs)
        {
            try
            {
                foreach (Reference reference in instance.GetReferences(referenceType))
                {
                    if (reference != null)
                        references.Add(reference);
                }
            }
            catch (Exception ex)
            {
                logs?.Add($"Opening {instance.Id.Value} (Category: {instance.Category?.Name}) missing '{referenceType}' reference plane. Error: {ex.Message}");
            }
        }
    }
}
