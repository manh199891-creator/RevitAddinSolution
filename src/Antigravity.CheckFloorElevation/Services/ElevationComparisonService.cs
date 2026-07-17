using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.CheckFloorElevation.Models;
using Antigravity.Core.Services;

namespace Antigravity.CheckFloorElevation.Services
{
    public class ElevationComparisonService
    {
        private const double MaxComparableDeltaMm = 500.0;
        private const double MaxProbeRayDistanceFeet = 10.0;

        private readonly Document _hostDoc;
        private readonly RevitLinkInstance _linkInstance;
        private readonly Document _linkedDoc;
        private readonly Transform _linkTransform;
        private readonly View3D _view3D;

        public ElevationComparisonService(Document hostDoc, RevitLinkInstance linkInstance, View3D view3D)
        {
            _hostDoc = hostDoc;
            _linkInstance = linkInstance;
            _linkedDoc = linkInstance.GetLinkDocument();
            _linkTransform = linkInstance.GetTotalTransform();
            _view3D = view3D;
        }

        public List<FloorCheckResult> RunCheck(
            IList<Floor> hostFloors,
            double toleranceMm,
            Action<int, int> progressCallback)
        {
            var results = new List<FloorCheckResult>();
            if (hostFloors == null || hostFloors.Count == 0)
                return results;

            PrepareViewForRaycast();

            var intersector = new ReferenceIntersector(_view3D)
            {
                FindReferencesInRevitLinks = true
            };

            for (int i = 0; i < hostFloors.Count; i++)
            {
                progressCallback?.Invoke(i + 1, hostFloors.Count);
                results.Add(CheckOneFloor(hostFloors[i], toleranceMm, intersector));
            }

            return results;
        }

        private FloorCheckResult CheckOneFloor(
            Floor hostFloor,
            double toleranceMm,
            ReferenceIntersector intersector)
        {
            var result = new FloorCheckResult
            {
                HostFloorId = ToInt32(hostFloor.Id),
                LinkFloorId = -1,
                LevelName = GetLevelName(hostFloor),
                HostTypeName = GetTypeName(hostFloor),
                LinkTypeName = string.Empty
            };

            try
            {
                double? zTopHostFeet = ElevationService.GetZTopFeet(hostFloor, Transform.Identity);
                if (!zTopHostFeet.HasValue)
                {
                    result.IsNoMatch = true;
                    result.ErrorMessage = "Cannot read host floor top elevation.";
                    return result;
                }

                result.ZTopHostMm = ElevationService.FeetToMillimeters(zTopHostFeet.Value);

                IList<XYZ> origins = ElevationService.GetProbePointsAbove(hostFloor, 2.0);
                if (origins == null || origins.Count == 0)
                {
                    result.IsNoMatch = true;
                    result.ErrorMessage = "Host floor has no bounding box.";
                    return result;
                }

                Floor linkedFloor = FindLinkedFloor(intersector, origins, result.ZTopHostMm);
                if (linkedFloor == null)
                {
                    result.IsNoMatch = true;
                    result.ErrorMessage = "No linked structural floor found within " + MaxComparableDeltaMm.ToString("0") + " mm below host floor probe points in 3D view: " + _view3D.Name;
                    return result;
                }

                result.LinkFloorId = ToInt32(linkedFloor.Id);
                result.LinkTypeName = GetTypeName(linkedFloor);
                double? zTopLinkFeet = ElevationService.GetZTopFeet(linkedFloor, _linkTransform);
                if (!zTopLinkFeet.HasValue)
                {
                    result.IsNoMatch = true;
                    result.ErrorMessage = "Cannot read linked floor top elevation.";
                    return result;
                }

                result.ZTopLinkMm = ElevationService.FeetToMillimeters(zTopLinkFeet.Value);
                result.IsError = Math.Abs(result.DeltaZMm) > toleranceMm;
                return result;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "[CheckFloorElevation] Floor comparison failed");
                result.IsNoMatch = true;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private Floor FindLinkedFloor(ReferenceIntersector intersector, IList<XYZ> origins, double zTopHostMm)
        {
            foreach (XYZ origin in origins)
            {
                Floor linkedFloor = FindLinkedFloorAtOrigin(intersector, origin, zTopHostMm);
                if (linkedFloor != null)
                    return linkedFloor;
            }

            return null;
        }

        private Floor FindLinkedFloorAtOrigin(ReferenceIntersector intersector, XYZ origin, double zTopHostMm)
        {
            IList<ReferenceWithContext> hits = intersector.Find(origin, XYZ.BasisZ.Negate());
            if (hits == null || hits.Count == 0)
                return null;

            foreach (ReferenceWithContext hit in hits.OrderBy(h => h.Proximity))
            {
                if (hit.Proximity > MaxProbeRayDistanceFeet)
                    break;

                Reference reference = hit.GetReference();
                if (reference == null)
                    continue;

                ElementId hostElementId = reference.ElementId;
                ElementId linkedElementId = reference.LinkedElementId;

                if (hostElementId != _linkInstance.Id)
                    continue;

                if (linkedElementId == ElementId.InvalidElementId)
                    continue;

                Element linkedElement = _linkedDoc.GetElement(linkedElementId);
                if (linkedElement is Floor linkedFloor)
                {
                    double? zTopLinkFeet = ElevationService.GetZTopFeet(linkedFloor, _linkTransform);
                    if (!zTopLinkFeet.HasValue)
                        continue;

                    double zTopLinkMm = ElevationService.FeetToMillimeters(zTopLinkFeet.Value);
                    if (Math.Abs(zTopHostMm - zTopLinkMm) <= MaxComparableDeltaMm)
                        return linkedFloor;
                }
            }

            return null;
        }

        private void PrepareViewForRaycast()
        {
            try
            {
                bool needsTransaction = _view3D.IsTemporaryHideIsolateActive() || _view3D.IsSectionBoxActive;
                if (!needsTransaction)
                    return;

                using (var transaction = new Transaction(_hostDoc, "Prepare 3D view for floor elevation check"))
                {
                    transaction.Start();

                    if (_view3D.IsTemporaryHideIsolateActive())
                        _view3D.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);

                    if (_view3D.IsSectionBoxActive)
                        _view3D.IsSectionBoxActive = false;

                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "[CheckFloorElevation] Failed to prepare 3D view for raycast");
            }
        }

        private string GetLevelName(Floor floor)
        {
            try
            {
                Level level = _hostDoc.GetElement(floor.LevelId) as Level;
                return level == null ? string.Empty : level.Name;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetTypeName(Floor floor)
        {
            try
            {
                ElementType type = floor.Document.GetElement(floor.GetTypeId()) as ElementType;
                return type == null ? string.Empty : type.Name;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int ToInt32(ElementId elementId)
        {
            return Convert.ToInt32(elementId.Value);
        }
    }
}
