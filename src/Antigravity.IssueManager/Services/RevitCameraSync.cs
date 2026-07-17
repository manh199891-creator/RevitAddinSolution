using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Autodesk.Revit.UI;
using Antigravity.IssueManager.Models;

namespace Antigravity.IssueManager.Services
{
    public class RevitCameraSync
    {
        private static readonly bool ShowSectionBoxDebugDialog = false;
        private const double MaxCameraRayTargetDistanceMeters = 150.0;
        private const double CameraRayFallbackDistanceMeters = 15.0;
        private const double SteepCameraRayFallbackDistanceMeters = 5.0;
        private const double SlopedCameraRayFallbackDistanceMeters = 8.0;
        private const double SteepCameraRayFallbackMaxVerticalDeltaMeters = 5.0;
        private const double SlopedCameraRayFallbackMaxVerticalDeltaMeters = 5.0;
        private const double DefaultCameraBoxWidthMeters = 15.0;
        private const double DefaultCameraBoxDepthMeters = 20.0;
        private const double DefaultCameraBoxHeightMeters = 6.0;
        private const double CameraRayFallbackBoxWidthMeters = 15.0;
        private const double CameraRayFallbackBoxDepthMeters = 20.0;
        private const double CameraRayFallbackBoxHeightMeters = 6.0;
        private const double SectionBoxViewportZoomFactor = 0.35;
        private const bool EnableExpensiveIfcParameterFallback = false;
        private static readonly Dictionary<int, BoundingBoxXYZ> ModelBoundingBoxCache = new Dictionary<int, BoundingBoxXYZ>();
        private static readonly Dictionary<int, ElementId> BcfViewCache = new Dictionary<int, ElementId>();
        private static readonly Dictionary<int, IDictionary<IFCGuidKey, ElementId>> IfcGuidMapCache =
            new Dictionary<int, IDictionary<IFCGuidKey, ElementId>>();
        private static readonly Dictionary<int, IDictionary<string, ElementId>> IfcParameterMapCache =
            new Dictionary<int, IDictionary<string, ElementId>>();

        public static void SyncCamera(UIApplication uiApp, ViewpointModel viewpoint)
        {
            if (viewpoint == null) return;
            SyncTiming timing = new SyncTiming();

            try
            {
                UIDocument uidoc = uiApp.ActiveUIDocument;
                Document doc = uidoc.Document;
                timing.Mark("init document");

                // 1. Match elements
                List<MatchedElement> matchedElements = new List<MatchedElement>();
                List<ElementId> hostIdsToSelect = new List<ElementId>();
                List<string> identifiers = GetElementIdentifiers(viewpoint);
                timing.Mark($"collect identifiers ({identifiers.Count})");

                IDictionary<IFCGuidKey, ElementId> ifcGuidMap = NeedsIfcGuidMap(identifiers)
                    ? GetOrCreateIfcGuidMap(doc)
                    : null;
                timing.Mark(ifcGuidMap == null ? "skip IFC GUID map" : $"resolve IFC GUID map ({ifcGuidMap.Count})");

                foreach (string idStr in identifiers)
                {
                    MatchedElement match = FindElementByStringIdentifier(doc, idStr, ifcGuidMap);
                    if (match != null)
                    {
                        AddMatchedElement(matchedElements, match);
                        if (!match.IsLinked)
                        {
                            AddElementId(hostIdsToSelect, match.Element.Id);
                        }
                        System.Diagnostics.Debug.WriteLine(
                            $"BCF matched: {idStr} -> {(match.IsLinked ? "Link" : "Host")} Element {match.Element.Id}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"BCF unmatched component: {idStr}");
                    }
                }
                timing.Mark(
                    $"match elements (matched={matchedElements.Count}, hostSelect={hostIdsToSelect.Count}, " +
                    $"ifcParamFallback={(EnableExpensiveIfcParameterFallback ? "on" : "off")})");

                bool useClashPointFallback =
                    viewpoint.HasClashPoint &&
                    identifiers.Count >= 2 &&
                    matchedElements.Count > 0 &&
                    matchedElements.Count < identifiers.Count;
                bool hasUsableClippingPlanes = HasUsableClippingPlanes(viewpoint);
                if (useClashPointFallback)
                {
                    AppendSyncLog(
                        $"Partial element match: identifiers={identifiers.Count}, matched={matchedElements.Count}. " +
                        "Using ClashPoint section box instead of isolating one element.");
                }

                List<ElementId> visibilityIds = GetVisibilityElementIds(matchedElements);
                List<ElementId> idsToSelect = hostIdsToSelect.Count > 0
                    ? hostIdsToSelect
                    : GetLinkedInstanceIds(matchedElements);

                if (idsToSelect.Count > 0)
                {
                    uidoc.Selection.SetElementIds(idsToSelect);
                }
                timing.Mark(idsToSelect.Count > 0 ? $"set selection ({idsToSelect.Count})" : "skip selection");

                // 2. Get or Create BCF View
                View3D targetView = GetOrCreateBcfView(doc);
                timing.Mark(targetView == null ? "get/create BCF view failed" : "get/create BCF view");
                if (targetView == null)
                {
                    TaskDialog.Show("Revit Sync", "Failed to create BCF 3D View.");
                    return;
                }

                if (uidoc.ActiveView.Id != targetView.Id)
                {
                    uidoc.ActiveView = targetView;
                    timing.Mark("activate BCF view");
                }
                else
                {
                    timing.Mark("BCF view already active");
                }

                // 3. Isolate or Reset
                bool hasLinkedMatches = matchedElements.Any(e => e.IsLinked);
                bool needsDisableTemp = targetView.IsTemporaryHideIsolateActive();
                bool needsIsolate = visibilityIds.Count > 0 && !useClashPointFallback && !hasUsableClippingPlanes;
                if (needsDisableTemp || needsIsolate)
                {
                    using (Transaction t = new Transaction(doc, "BCF Element Visibility"))
                    {
                        t.Start();
                        if (needsDisableTemp)
                        {
                            targetView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                        }

                        if (needsIsolate)
                        {
                            targetView.IsolateElementsTemporary(visibilityIds);
                        }
                        t.Commit();
                    }
                    timing.Mark(
                        $"visibility transaction (disable={needsDisableTemp}, isolate={needsIsolate}, " +
                        $"ids={visibilityIds.Count}, linked={hasLinkedMatches})");
                }
                else
                {
                    timing.Mark("skip visibility transaction");
                }

                // 4. Section Box
                if (uidoc.ActiveView is View3D view3d)
                {
                    if (matchedElements.Count > 0 && !useClashPointFallback && !hasUsableClippingPlanes)
                    {
                        // Element matched → Section box quanh element
                        if (ApplySectionBoxAroundElements(view3d, matchedElements) && ZoomToSectionBox(uidoc, view3d))
                        {
                            timing.Mark("apply matched element section box + zoom");
                            return;
                        }
                        if (hostIdsToSelect.Count > 0)
                        {
                            uidoc.ShowElements(hostIdsToSelect);
                        }
                        timing.Mark("matched element fallback show elements");
                        return;
                    }

                    // No element match: create an oriented section box from the BCF camera.
                    Transform locTransform = doc.ActiveProjectLocation.GetTransform();
                    CoordinateConversion conversion = ResolveCoordinateConversion(doc, viewpoint, locTransform);
                    timing.Mark($"resolve coordinate conversion ({conversion.ModeName}, {conversion.TargetSource})");

                    XYZ boxOriginInternal = conversion.Target;
                    XYZ fwd = conversion.Forward;
                    XYZ up = conversion.Up;

                    if (fwd.IsZeroLength()) fwd = XYZ.BasisY;
                    if (up.IsZeroLength()) up = XYZ.BasisZ;

                    fwd = fwd.Normalize();
                    up = up.Normalize();

                    XYZ right = fwd.CrossProduct(up);
                    if (right.IsZeroLength())
                    {
                        XYZ fallbackUp = Math.Abs(fwd.DotProduct(XYZ.BasisZ)) < 0.99 ? XYZ.BasisZ : XYZ.BasisX;
                        right = fwd.CrossProduct(fallbackUp);
                    }
                    right = right.Normalize();
                    up = right.CrossProduct(fwd).Normalize();
                    timing.Mark("build oriented basis");

                    bool exactClippingBox = TryCreateSectionBoxFromClippingPlanes(
                        conversion.ClippingPlanes,
                        out BoundingBoxXYZ orientedBox,
                        out double halfWidth,
                        out double halfDepth,
                        out double halfHeight);
                    if (exactClippingBox)
                    {
                        boxOriginInternal = orientedBox.Transform.Origin;
                        conversion.Target = boxOriginInternal;
                        conversion.TargetSource = "Exact BCF clipping planes";
                        timing.Mark("create exact clipping plane section box");
                    }
                    else
                    {
                        ResolveFallbackBoxSize(viewpoint, conversion.TargetSource, out halfWidth, out halfDepth, out halfHeight);

                        Transform boxTransform = Transform.Identity;
                        boxTransform.BasisX = right;
                        boxTransform.BasisY = fwd;
                        boxTransform.BasisZ = up;
                        boxTransform.Origin = boxOriginInternal;

                        orientedBox = new BoundingBoxXYZ
                        {
                            Transform = boxTransform,
                            Min = new XYZ(-halfWidth, -halfDepth, -halfHeight),
                            Max = new XYZ(halfWidth, halfDepth, halfHeight)
                        };
                        timing.Mark("create fallback oriented section box");
                    }

                    GetWorldExtents(orientedBox, out XYZ sectionWorldMin, out XYZ sectionWorldMax);

                    WriteSectionBoxDebug(
                        viewpoint,
                        conversion,
                        locTransform,
                        boxOriginInternal,
                        fwd,
                        up,
                        right,
                        halfWidth,
                        halfDepth,
                        halfHeight,
                        sectionWorldMin,
                        sectionWorldMax);
                    timing.Mark("write camera debug log");

                    using (Transaction t2 = new Transaction(doc, "Apply BCF Oriented Section Box"))
                    {
                        t2.Start();
                        ApplyCameraOrientation(view3d, conversion.Camera, boxOriginInternal, up, fwd, conversion.TargetSource);
                        view3d.SetSectionBox(orientedBox);
                        view3d.IsSectionBoxActive = true;
                        t2.Commit();
                    }
                    timing.Mark("apply orientation + section box transaction");

                    ZoomToSectionBox(uidoc, view3d, exactClippingBox ? 0.90 : SectionBoxViewportZoomFactor);
                    timing.Mark("zoom to section box");

                    ExportViewCompareSnapshot(doc, view3d, viewpoint);
                    timing.Mark("export Revit view compare snapshot");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Camera Sync error: " + ex.Message);
                AppendSyncLog("Camera Sync error: " + ex);
            }
            finally
            {
                AppendSyncLog(timing.ToLogString());
            }
        }

        private static void DisableSectionBox(Document doc, View3D view3d)
        {
            if (doc == null || view3d == null || !view3d.IsSectionBoxActive) return;
            using (Transaction t = new Transaction(doc, "Disable BCF Section Box"))
            {
                t.Start();
                view3d.IsSectionBoxActive = false;
                t.Commit();
            }
        }

        private static void WriteSectionBoxDebug(
            ViewpointModel viewpoint,
            CoordinateConversion conversion,
            Transform locTransform,
            XYZ boxOriginInternal,
            XYZ fwd,
            XYZ up,
            XYZ right,
            double halfWidth,
            double halfDepth,
            double halfHeight,
            XYZ sectionWorldMin,
            XYZ sectionWorldMax)
        {
            double toM = 0.3048;
            double cameraTargetDistanceMeters = conversion.Camera.DistanceTo(boxOriginInternal) * toM;
            double targetDeltaZMeters = (boxOriginInternal.Z - conversion.Camera.Z) * toM;
            double sectionCenterToModelMeters = conversion.ModelBox == null
                ? 0
                : DistanceToBox(boxOriginInternal, conversion.ModelBox) * toM;
            string modelBoundsText = conversion.ModelBox == null
                ? "(none)"
                : $"Min({conversion.ModelBox.Min.X * toM:F1}, {conversion.ModelBox.Min.Y * toM:F1}, {conversion.ModelBox.Min.Z * toM:F1})m, " +
                  $"Max({conversion.ModelBox.Max.X * toM:F1}, {conversion.ModelBox.Max.Y * toM:F1}, {conversion.ModelBox.Max.Z * toM:F1})m";
            string debugMsg =
                $"=== BCF Camera Input (meters) ===\n" +
                $"Camera XYZ: ({viewpoint.CameraX:F2}, {viewpoint.CameraY:F2}, {viewpoint.CameraZ:F2})\n" +
                $"Direction:  ({viewpoint.CameraDirectionX:F4}, {viewpoint.CameraDirectionY:F4}, {viewpoint.CameraDirectionZ:F4})\n" +
                $"Up:         ({viewpoint.CameraUpX:F4}, {viewpoint.CameraUpY:F4}, {viewpoint.CameraUpZ:F4})\n" +
                $"ClashPoint: ({viewpoint.ClashPointX:F2}, {viewpoint.ClashPointY:F2}, {viewpoint.ClashPointZ:F2})\n" +
                $"HasClashPoint: {viewpoint.HasClashPoint}\n" +
                $"Coordinate Mode: {conversion.ModeName}\n" +
                $"Target Source: {conversion.TargetSource}\n" +
                $"Distance To Model: {conversion.DistanceToModel * toM:F1} m\n" +
                $"Selection Score: {conversion.SelectionScore * toM:F1} m\n" +
                $"\n=== After Transform (feet, internal) ===\n" +
                $"Camera: ({conversion.Camera.X:F1}, {conversion.Camera.Y:F1}, {conversion.Camera.Z:F1}) ft\n" +
                $"Camera: ({conversion.Camera.X * toM:F1}, {conversion.Camera.Y * toM:F1}, {conversion.Camera.Z * toM:F1}) m\n" +
                $"Box Origin: ({boxOriginInternal.X:F1}, {boxOriginInternal.Y:F1}, {boxOriginInternal.Z:F1}) ft\n" +
                $"Box Origin: ({boxOriginInternal.X * toM:F1}, {boxOriginInternal.Y * toM:F1}, {boxOriginInternal.Z * toM:F1}) m\n" +
                $"Camera->Target: {cameraTargetDistanceMeters:F1} m\n" +
                $"Target Delta Z: {targetDeltaZMeters:F1} m\n" +
                $"Fwd:   ({fwd.X:F3}, {fwd.Y:F3}, {fwd.Z:F3})\n" +
                $"Up:    ({up.X:F3}, {up.Y:F3}, {up.Z:F3})\n" +
                $"Right: ({right.X:F3}, {right.Y:F3}, {right.Z:F3})\n" +
                $"Fwd vertical abs: {Math.Abs(fwd.Z):F3}, Up vertical: {up.Z:F3}\n" +
                $"\n=== Section Box Size (meters) ===\n" +
                $"Width: ±{halfWidth * toM:F1}m, Depth: ±{halfDepth * toM:F1}m, Height: ±{halfHeight * toM:F1}m\n" +
                $"World Min: ({sectionWorldMin.X * toM:F1}, {sectionWorldMin.Y * toM:F1}, {sectionWorldMin.Z * toM:F1}) m\n" +
                $"World Max: ({sectionWorldMax.X * toM:F1}, {sectionWorldMax.Y * toM:F1}, {sectionWorldMax.Z * toM:F1}) m\n" +
                $"Section Center -> Model: {sectionCenterToModelMeters:F1} m\n" +
                $"Model Bounds: {modelBoundsText}\n" +
                $"\n=== ProjectLocation Transform ===\n" +
                $"Origin: ({locTransform.Origin.X:F1}, {locTransform.Origin.Y:F1}, {locTransform.Origin.Z:F1}) ft\n" +
                $"Origin: ({locTransform.Origin.X * toM:F1}, {locTransform.Origin.Y * toM:F1}, {locTransform.Origin.Z * toM:F1}) m";

            System.Diagnostics.Debug.WriteLine(debugMsg);
            AppendSyncLog(debugMsg);
            if (ShowSectionBoxDebugDialog)
            {
                TaskDialog.Show("BCF Section Box Debug", debugMsg);
            }
        }

        private static void ApplyCameraOrientation(
            View3D view3d,
            XYZ camera,
            XYZ target,
            XYZ up,
            XYZ forward,
            string targetSource)
        {
            XYZ eye = camera;
            XYZ cameraToTarget = target - camera;
            if (cameraToTarget.IsZeroLength() ||
                (IsClashPointTarget(targetSource) && camera.GetLength() < UnitUtils.ConvertToInternalUnits(1.0, UnitTypeId.Meters)))
            {
                double fallbackDistance = UnitUtils.ConvertToInternalUnits(15.0, UnitTypeId.Meters);
                eye = target - forward.Normalize() * fallbackDistance;
            }

            ViewOrientation3D orientation = new ViewOrientation3D(eye, up, forward);
            view3d.SetOrientation(orientation);

            string logMessage =
                $"BCF ViewOrientation applied: eye=({eye.X:F1},{eye.Y:F1},{eye.Z:F1})ft, " +
                $"target=({target.X:F1},{target.Y:F1},{target.Z:F1})ft, " +
                $"forward=({forward.X:F3},{forward.Y:F3},{forward.Z:F3}), " +
                $"up=({up.X:F3},{up.Y:F3},{up.Z:F3}), " +
                $"targetSource={targetSource}, " +
                $"distance={eye.DistanceTo(target) * 0.3048:F1}m";
            System.Diagnostics.Debug.WriteLine(logMessage);
            AppendSyncLog(logMessage);
        }

        private static void AppendSyncLog(string message)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string logDir = Path.Combine(localAppData, "AntigravityIssueManager");
                Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, "bcf_camera_sync.log");
                File.AppendAllText(
                    logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // Logging should never block Show in Model.
            }
        }

        private static CoordinateConversion ResolveCoordinateConversion(
            Document doc,
            ViewpointModel viewpoint,
            Transform projectLocationTransform)
        {
            XYZ sourceCamera = ToInternalPointMeters(viewpoint.CameraX, viewpoint.CameraY, viewpoint.CameraZ);
            bool hasExplicitTarget = TryResolveExplicitSourceTarget(viewpoint, out XYZ sourceTarget, out string targetSource);
            XYZ sourceForward = new XYZ(
                viewpoint.CameraDirectionX,
                viewpoint.CameraDirectionY,
                viewpoint.CameraDirectionZ);
            XYZ sourceUp = new XYZ(
                viewpoint.CameraUpX,
                viewpoint.CameraUpY,
                viewpoint.CameraUpZ);

            Transform sharedToInternal = projectLocationTransform.Inverse;
            ProjectPosition projectPosition = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
            Transform projectPositionRotation = Transform.CreateRotation(XYZ.BasisZ, -projectPosition.Angle);
            XYZ projectPositionOffset = new XYZ(
                projectPosition.EastWest,
                projectPosition.NorthSouth,
                projectPosition.Elevation);

            List<CoordinateConversion> candidates = new List<CoordinateConversion>
            {
                new CoordinateConversion
                {
                    ModeName = "Shared (ProjectPosition inverse)",
                    Camera = projectPositionRotation.OfPoint(sourceCamera - projectPositionOffset),
                    Target = hasExplicitTarget ? projectPositionRotation.OfPoint(sourceTarget - projectPositionOffset) : null,
                    Forward = projectPositionRotation.OfVector(sourceForward),
                    Up = projectPositionRotation.OfVector(sourceUp),
                    ClippingPlanes = ConvertClippingPlanes(
                        viewpoint,
                        p => projectPositionRotation.OfPoint(p - projectPositionOffset),
                        v => projectPositionRotation.OfVector(v)),
                    TargetSource = targetSource
                },
                new CoordinateConversion
                {
                    ModeName = "Shared (ProjectLocation.GetTransform inverse)",
                    Camera = sharedToInternal.OfPoint(sourceCamera),
                    Target = hasExplicitTarget ? sharedToInternal.OfPoint(sourceTarget) : null,
                    Forward = sharedToInternal.OfVector(sourceForward),
                    Up = sharedToInternal.OfVector(sourceUp),
                    ClippingPlanes = ConvertClippingPlanes(
                        viewpoint,
                        p => sharedToInternal.OfPoint(p),
                        v => sharedToInternal.OfVector(v)),
                    TargetSource = targetSource
                },
                new CoordinateConversion
                {
                    ModeName = "Internal (Raw model coordinates)",
                    Camera = sourceCamera,
                    Target = hasExplicitTarget ? sourceTarget : null,
                    Forward = sourceForward,
                    Up = sourceUp,
                    ClippingPlanes = ConvertClippingPlanes(viewpoint, p => p, v => v),
                    TargetSource = targetSource
                }
            };

            if (!hasExplicitTarget)
            {
                candidates.Add(new CoordinateConversion
                {
                    ModeName = "Shared (ProjectPosition inverse, direction flipped)",
                    Camera = projectPositionRotation.OfPoint(sourceCamera - projectPositionOffset),
                    Target = null,
                    Forward = projectPositionRotation.OfVector(sourceForward.Negate()),
                    Up = projectPositionRotation.OfVector(sourceUp.Negate()),
                    ClippingPlanes = ConvertClippingPlanes(
                        viewpoint,
                        p => projectPositionRotation.OfPoint(p - projectPositionOffset),
                        v => projectPositionRotation.OfVector(v)),
                    TargetSource = targetSource
                });
                candidates.Add(new CoordinateConversion
                {
                    ModeName = "Shared (ProjectLocation.GetTransform inverse, direction flipped)",
                    Camera = sharedToInternal.OfPoint(sourceCamera),
                    Target = null,
                    Forward = sharedToInternal.OfVector(sourceForward.Negate()),
                    Up = sharedToInternal.OfVector(sourceUp.Negate()),
                    ClippingPlanes = ConvertClippingPlanes(
                        viewpoint,
                        p => sharedToInternal.OfPoint(p),
                        v => sharedToInternal.OfVector(v)),
                    TargetSource = targetSource
                });
                candidates.Add(new CoordinateConversion
                {
                    ModeName = "Internal (Raw model coordinates, direction flipped)",
                    Camera = sourceCamera,
                    Target = null,
                    Forward = sourceForward.Negate(),
                    Up = sourceUp.Negate(),
                    ClippingPlanes = ConvertClippingPlanes(viewpoint, p => p, v => v),
                    TargetSource = targetSource
                });
            }

            BoundingBoxXYZ modelBox = GetOrCreateModelBoundingBox(doc);
            foreach (CoordinateConversion candidate in candidates)
            {
                candidate.ModelBox = modelBox;
                ResolveCandidateTarget(candidate, modelBox);
                candidate.DistanceToModel = modelBox == null
                    ? candidate.Target.GetLength()
                    : DistanceToBox(candidate.Target, modelBox);
                candidate.SelectionScore = candidate.DistanceToModel + GetViewOrientationPenalty(candidate);
            }

            IEnumerable<CoordinateConversion> preferredCandidates = FilterCandidatesByCoordinateMode(candidates, viewpoint?.CoordinateMode);
            CoordinateConversion selected = preferredCandidates.OrderBy(c => c.SelectionScore).FirstOrDefault()
                ?? candidates.OrderBy(c => c.SelectionScore).First();
            AppendSyncLog(
                $"Coordinate conversion selected: viewpointMode='{viewpoint?.CoordinateMode ?? "(empty)"}', " +
                $"selected='{selected.ModeName}', score={selected.SelectionScore * 0.3048:F1}m");
            return selected;
        }

        private static IEnumerable<CoordinateConversion> FilterCandidatesByCoordinateMode(
            List<CoordinateConversion> candidates,
            string coordinateMode)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return Enumerable.Empty<CoordinateConversion>();
            }

            if (string.Equals(coordinateMode, "Internal", StringComparison.OrdinalIgnoreCase))
            {
                return candidates.Where(c => c.ModeName.StartsWith("Internal", StringComparison.OrdinalIgnoreCase));
            }

            if (string.Equals(coordinateMode, "Shared", StringComparison.OrdinalIgnoreCase))
            {
                return candidates.Where(c => c.ModeName.StartsWith("Shared", StringComparison.OrdinalIgnoreCase));
            }

            return candidates;
        }

        private static double GetViewOrientationPenalty(CoordinateConversion candidate)
        {
            if (candidate?.Forward == null || candidate.Forward.IsZeroLength())
            {
                return 0;
            }

            double penaltyMeters = 0;
            XYZ forward = candidate.Forward.Normalize();
            XYZ up = candidate.Up == null || candidate.Up.IsZeroLength()
                ? XYZ.BasisZ
                : candidate.Up.Normalize();

            if (forward.Z > 0.05)
            {
                penaltyMeters += 1000.0;
            }

            if (up.Z < -0.2)
            {
                penaltyMeters += 500.0;
            }

            return UnitUtils.ConvertToInternalUnits(penaltyMeters, UnitTypeId.Meters);
        }

        private static bool TryResolveExplicitSourceTarget(ViewpointModel viewpoint, out XYZ target, out string targetSource)
        {
            if (viewpoint.HasClashPoint)
            {
                target = ToInternalPointMeters(
                    viewpoint.ClashPointX,
                    viewpoint.ClashPointY,
                    viewpoint.ClashPointZ);
                targetSource = "ClashPoint";
                return true;
            }

            if (TryGetClippingPlaneCenter(viewpoint, out XYZ clippingCenter))
            {
                target = clippingCenter;
                targetSource = "ClippingPlanes center";
                return true;
            }

            target = null;
            targetSource = "Camera ray";
            return false;
        }

        private static void ResolveCandidateTarget(
            CoordinateConversion candidate,
            BoundingBoxXYZ modelBox)
        {
            if (candidate.Target != null)
            {
                return;
            }

            XYZ forward = candidate.Forward;
            if (forward == null || forward.IsZeroLength())
            {
                forward = XYZ.BasisY;
            }
            forward = forward.Normalize();

            if (modelBox != null && TryIntersectRayWithBox(candidate.Camera, forward, modelBox, out XYZ rayTarget))
            {
                double rayDistanceMeters = candidate.Camera.DistanceTo(rayTarget) * 0.3048;
                if (rayDistanceMeters <= MaxCameraRayTargetDistanceMeters)
                {
                    candidate.Target = rayTarget;
                    candidate.TargetSource = $"Camera ray -> model bbox ({rayDistanceMeters:F1}m)";
                    return;
                }

                AppendSyncLog(
                    $"Camera ray target rejected: mode={candidate.ModeName}, " +
                    $"distance={rayDistanceMeters:F1}m, max={MaxCameraRayTargetDistanceMeters:F1}m, " +
                    $"target=({rayTarget.X:F1},{rayTarget.Y:F1},{rayTarget.Z:F1})ft");
            }

            double fallbackDistanceMeters = GetCameraRayFallbackDistanceMeters(candidate.Camera, forward, modelBox, out string fallbackReason);
            double fallbackDistance = UnitUtils.ConvertToInternalUnits(fallbackDistanceMeters, UnitTypeId.Meters);
            candidate.Target = candidate.Camera + forward * fallbackDistance;
            candidate.TargetSource = $"Camera ray fallback {fallbackDistanceMeters:F1}m ({fallbackReason})";
        }

        private static double GetCameraRayFallbackDistanceMeters(
            XYZ camera,
            XYZ forward,
            BoundingBoxXYZ modelBox,
            out string reason)
        {
            double vertical = Math.Abs(forward.Normalize().Z);
            if (vertical >= 0.75)
            {
                double byZ = SteepCameraRayFallbackMaxVerticalDeltaMeters / vertical;
                double distance = Clamp(byZ, 4.0, 8.0);
                reason = "steep z-clamped";
                return distance;
            }

            if (vertical >= 0.45)
            {
                double byZ = SlopedCameraRayFallbackMaxVerticalDeltaMeters / vertical;
                double distance = Clamp(byZ, 5.0, Math.Max(SlopedCameraRayFallbackDistanceMeters, 12.0));
                reason = "sloped z-clamped";
                return distance;
            }

            if (modelBox != null && camera != null)
            {
                double distanceToModelMeters = DistanceToBox(camera, modelBox) * 0.3048;
                if (distanceToModelMeters > 0 && distanceToModelMeters < CameraRayFallbackDistanceMeters)
                {
                    reason = "near model";
                    return Clamp(distanceToModelMeters, 5.0, CameraRayFallbackDistanceMeters);
                }
            }

            reason = "default";
            return CameraRayFallbackDistanceMeters;
        }

        private static bool TryGetClippingPlaneCenter(
            ViewpointModel viewpoint,
            out XYZ center)
        {
            center = null;
            if (viewpoint?.ClippingPlanes == null || viewpoint.ClippingPlanes.Count < 2)
            {
                return false;
            }

            XYZ sum = XYZ.Zero;
            int count = 0;
            foreach (ClippingPlaneModel plane in viewpoint.ClippingPlanes)
            {
                XYZ location = ToInternalPointMeters(plane.LocationX, plane.LocationY, plane.LocationZ);
                sum += location;
                count++;
            }

            if (count < 2)
            {
                return false;
            }

            center = sum / count;
            return true;
        }

        private static bool HasUsableClippingPlanes(ViewpointModel viewpoint)
        {
            if (viewpoint?.ClippingPlanes == null || viewpoint.ClippingPlanes.Count < 6)
            {
                return false;
            }

            int validCount = 0;
            foreach (ClippingPlaneModel plane in viewpoint.ClippingPlanes)
            {
                XYZ direction = new XYZ(plane.DirectionX, plane.DirectionY, plane.DirectionZ);
                if (IsFinitePoint(plane.LocationX, plane.LocationY, plane.LocationZ) &&
                    IsFiniteVector(direction) &&
                    !direction.IsZeroLength())
                {
                    validCount++;
                }
            }

            return validCount >= 6;
        }

        private static List<ConvertedClippingPlane> ConvertClippingPlanes(
            ViewpointModel viewpoint,
            Func<XYZ, XYZ> pointTransform,
            Func<XYZ, XYZ> vectorTransform)
        {
            List<ConvertedClippingPlane> converted = new List<ConvertedClippingPlane>();
            if (viewpoint?.ClippingPlanes == null)
            {
                return converted;
            }

            foreach (ClippingPlaneModel plane in viewpoint.ClippingPlanes)
            {
                if (!IsFinitePoint(plane.LocationX, plane.LocationY, plane.LocationZ))
                {
                    continue;
                }

                XYZ sourceLocation = ToInternalPointMeters(plane.LocationX, plane.LocationY, plane.LocationZ);
                XYZ sourceDirection = new XYZ(plane.DirectionX, plane.DirectionY, plane.DirectionZ);
                if (!IsFiniteVector(sourceDirection) || sourceDirection.IsZeroLength())
                {
                    continue;
                }

                XYZ convertedDirection = vectorTransform(sourceDirection);
                if (convertedDirection == null || convertedDirection.IsZeroLength())
                {
                    continue;
                }

                converted.Add(new ConvertedClippingPlane
                {
                    Location = pointTransform(sourceLocation),
                    Direction = convertedDirection.Normalize()
                });
            }

            return converted;
        }

        private static bool TryCreateSectionBoxFromClippingPlanes(
            IList<ConvertedClippingPlane> planes,
            out BoundingBoxXYZ sectionBox,
            out double halfWidth,
            out double halfDepth,
            out double halfHeight)
        {
            sectionBox = null;
            halfWidth = 0;
            halfDepth = 0;
            halfHeight = 0;

            if (planes == null || planes.Count < 6)
            {
                return false;
            }

            List<ClipPlanePair> pairs = PairOppositeClippingPlanes(planes);
            if (pairs.Count < 3)
            {
                return false;
            }

            List<ClipPlanePair> orthogonalPairs = SelectOrthogonalPlanePairs(pairs);
            if (orthogonalPairs.Count < 3)
            {
                return false;
            }

            XYZ axisX = orthogonalPairs[0].Axis;
            XYZ axisY = orthogonalPairs[1].Axis;
            XYZ axisZ = orthogonalPairs[2].Axis;
            double offsetX = orthogonalPairs[0].CenterOffset;
            double offsetY = orthogonalPairs[1].CenterOffset;
            double offsetZ = orthogonalPairs[2].CenterOffset;

            if (axisX.CrossProduct(axisY).DotProduct(axisZ) < 0)
            {
                axisZ = axisZ.Negate();
                offsetZ = -offsetZ;
            }

            XYZ center = axisX * offsetX + axisY * offsetY + axisZ * offsetZ;
            halfWidth = Math.Max(orthogonalPairs[0].HalfExtent, UnitUtils.ConvertToInternalUnits(0.05, UnitTypeId.Meters));
            halfDepth = Math.Max(orthogonalPairs[1].HalfExtent, UnitUtils.ConvertToInternalUnits(0.05, UnitTypeId.Meters));
            halfHeight = Math.Max(orthogonalPairs[2].HalfExtent, UnitUtils.ConvertToInternalUnits(0.05, UnitTypeId.Meters));

            Transform boxTransform = Transform.Identity;
            boxTransform.BasisX = axisX;
            boxTransform.BasisY = axisY;
            boxTransform.BasisZ = axisZ;
            boxTransform.Origin = center;

            sectionBox = new BoundingBoxXYZ
            {
                Transform = boxTransform,
                Min = new XYZ(-halfWidth, -halfDepth, -halfHeight),
                Max = new XYZ(halfWidth, halfDepth, halfHeight)
            };

            AppendSyncLog(
                $"Exact clipping box restored: center=({center.X:F1},{center.Y:F1},{center.Z:F1})ft, " +
                $"half=({halfWidth * 0.3048:F2},{halfDepth * 0.3048:F2},{halfHeight * 0.3048:F2})m");
            return true;
        }

        private static List<ClipPlanePair> PairOppositeClippingPlanes(IList<ConvertedClippingPlane> planes)
        {
            List<ClipPlanePair> pairs = new List<ClipPlanePair>();
            bool[] used = new bool[planes.Count];

            for (int i = 0; i < planes.Count; i++)
            {
                if (used[i] || planes[i]?.Direction == null || planes[i].Direction.IsZeroLength())
                {
                    continue;
                }

                XYZ axis = planes[i].Direction.Normalize();
                int bestIndex = -1;
                double bestOpposition = -1;
                for (int j = i + 1; j < planes.Count; j++)
                {
                    if (used[j] || planes[j]?.Direction == null || planes[j].Direction.IsZeroLength())
                    {
                        continue;
                    }

                    double opposition = -axis.DotProduct(planes[j].Direction.Normalize());
                    if (opposition > bestOpposition)
                    {
                        bestOpposition = opposition;
                        bestIndex = j;
                    }
                }

                if (bestIndex < 0 || bestOpposition < 0.95)
                {
                    continue;
                }

                double offsetA = axis.DotProduct(planes[i].Location);
                double offsetB = axis.DotProduct(planes[bestIndex].Location);
                pairs.Add(new ClipPlanePair
                {
                    Axis = axis,
                    CenterOffset = (offsetA + offsetB) * 0.5,
                    HalfExtent = Math.Abs(offsetA - offsetB) * 0.5
                });
                used[i] = true;
                used[bestIndex] = true;
            }

            return pairs;
        }

        private static List<ClipPlanePair> SelectOrthogonalPlanePairs(List<ClipPlanePair> pairs)
        {
            List<ClipPlanePair> selected = new List<ClipPlanePair>();
            foreach (ClipPlanePair pair in pairs.OrderByDescending(p => p.HalfExtent))
            {
                bool orthogonal = true;
                foreach (ClipPlanePair existing in selected)
                {
                    if (Math.Abs(pair.Axis.DotProduct(existing.Axis)) > 0.15)
                    {
                        orthogonal = false;
                        break;
                    }
                }

                if (orthogonal)
                {
                    selected.Add(pair);
                    if (selected.Count == 3)
                    {
                        break;
                    }
                }
            }

            return selected;
        }

        private static bool IsFinitePoint(double x, double y, double z)
        {
            return IsFiniteNumber(x) && IsFiniteNumber(y) && IsFiniteNumber(z);
        }

        private static bool IsFiniteVector(XYZ vector)
        {
            return vector != null &&
                   IsFiniteNumber(vector.X) &&
                   IsFiniteNumber(vector.Y) &&
                   IsFiniteNumber(vector.Z);
        }

        private static bool IsFiniteNumber(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static BoundingBoxXYZ CreateModelBoundingBox(Document doc)
        {
            bool hasBox = false;
            double minX = 0;
            double minY = 0;
            double minZ = 0;
            double maxX = 0;
            double maxY = 0;
            double maxZ = 0;

            foreach (Element element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
            {
                BoundingBoxXYZ box = null;
                Transform transform = Transform.Identity;

                if (element is RevitLinkInstance linkInstance)
                {
                    box = linkInstance.get_BoundingBox(null);
                    transform = linkInstance.GetTotalTransform();
                }
                else
                {
                    box = element.get_BoundingBox(null);
                }

                if (box == null)
                {
                    continue;
                }

                AddBoxToExtents(box, transform, ref hasBox, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            }

            if (!hasBox)
            {
                return null;
            }

            return new BoundingBoxXYZ
            {
                Transform = Transform.Identity,
                Min = new XYZ(minX, minY, minZ),
                Max = new XYZ(maxX, maxY, maxZ)
            };
        }

        private static BoundingBoxXYZ GetOrCreateModelBoundingBox(Document doc)
        {
            int key = GetDocumentCacheKey(doc);
            if (ModelBoundingBoxCache.TryGetValue(key, out BoundingBoxXYZ cachedBox))
            {
                return cachedBox;
            }

            BoundingBoxXYZ box = CreateModelBoundingBox(doc);
            ModelBoundingBoxCache[key] = box;
            return box;
        }

        private static void AddBoxToExtents(
            BoundingBoxXYZ box,
            Transform extraTransform,
            ref bool hasBox,
            ref double minX,
            ref double minY,
            ref double minZ,
            ref double maxX,
            ref double maxY,
            ref double maxZ)
        {
            Transform boxTransform = box.Transform ?? Transform.Identity;
            Transform transform = extraTransform == null
                ? boxTransform
                : extraTransform.Multiply(boxTransform);

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

            foreach (XYZ corner in corners.Select(transform.OfPoint))
            {
                if (!hasBox)
                {
                    minX = maxX = corner.X;
                    minY = maxY = corner.Y;
                    minZ = maxZ = corner.Z;
                    hasBox = true;
                    continue;
                }

                minX = Math.Min(minX, corner.X);
                minY = Math.Min(minY, corner.Y);
                minZ = Math.Min(minZ, corner.Z);
                maxX = Math.Max(maxX, corner.X);
                maxY = Math.Max(maxY, corner.Y);
                maxZ = Math.Max(maxZ, corner.Z);
            }
        }

        private static double DistanceToBox(XYZ point, BoundingBoxXYZ box)
        {
            double dx = DistanceOutsideInterval(point.X, box.Min.X, box.Max.X);
            double dy = DistanceOutsideInterval(point.Y, box.Min.Y, box.Max.Y);
            double dz = DistanceOutsideInterval(point.Z, box.Min.Z, box.Max.Z);
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double DistanceOutsideInterval(double value, double min, double max)
        {
            if (value < min) return min - value;
            if (value > max) return value - max;
            return 0;
        }

        private static bool TryIntersectRayWithBox(XYZ origin, XYZ direction, BoundingBoxXYZ box, out XYZ target)
        {
            return TryIntersectRayWithBox(origin, direction, box.Min, box.Max, out target);
        }

        private static bool TryIntersectRayWithBox(XYZ origin, XYZ direction, XYZ min, XYZ max, out XYZ target)
        {
            target = null;
            double tMin = double.NegativeInfinity;
            double tMax = double.PositiveInfinity;

            if (!ClipRaySlab(origin.X, direction.X, min.X, max.X, ref tMin, ref tMax) ||
                !ClipRaySlab(origin.Y, direction.Y, min.Y, max.Y, ref tMin, ref tMax) ||
                !ClipRaySlab(origin.Z, direction.Z, min.Z, max.Z, ref tMin, ref tMax))
            {
                return false;
            }

            if (tMax < 0)
            {
                return false;
            }

            double t;
            if (tMin >= 0)
            {
                t = (tMin + tMax) * 0.5;
            }
            else
            {
                t = tMax * 0.5;
            }

            target = origin + direction * t;
            return true;
        }

        private static bool ClipRaySlab(
            double origin,
            double direction,
            double min,
            double max,
            ref double tMin,
            ref double tMax)
        {
            const double epsilon = 1e-9;
            if (Math.Abs(direction) < epsilon)
            {
                return origin >= min && origin <= max;
            }

            double t1 = (min - origin) / direction;
            double t2 = (max - origin) / direction;
            if (t1 > t2)
            {
                double temp = t1;
                t1 = t2;
                t2 = temp;
            }

            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            return tMin <= tMax;
        }

        private static void ResolveFallbackBoxSize(
            ViewpointModel viewpoint,
            string targetSource,
            out double halfWidth,
            out double halfDepth,
            out double halfHeight)
        {
            double widthMeters = DefaultCameraBoxWidthMeters;
            double depthMeters = DefaultCameraBoxDepthMeters;
            double heightMeters = DefaultCameraBoxHeightMeters;

            if (IsCameraRayFallback(targetSource))
            {
                widthMeters = CameraRayFallbackBoxWidthMeters;
                depthMeters = CameraRayFallbackBoxDepthMeters;
                heightMeters = CameraRayFallbackBoxHeightMeters;
            }

            if (viewpoint.IsOrthogonal && viewpoint.ViewToWorldScale > 0)
            {
                double maxScaleMeters = IsCameraRayFallback(targetSource) ? 20.0 : 12.0;
                double scaleMeters = Clamp(viewpoint.ViewToWorldScale, 4.0, maxScaleMeters);
                widthMeters = Math.Max(widthMeters, scaleMeters);
                depthMeters = Math.Max(depthMeters, scaleMeters);
                heightMeters = Math.Max(heightMeters, scaleMeters * 0.3);
            }

            halfWidth = UnitUtils.ConvertToInternalUnits(widthMeters * 0.5, UnitTypeId.Meters);
            halfDepth = UnitUtils.ConvertToInternalUnits(depthMeters * 0.5, UnitTypeId.Meters);
            halfHeight = UnitUtils.ConvertToInternalUnits(heightMeters * 0.5, UnitTypeId.Meters);
        }

        private static bool IsCameraRayFallback(string targetSource)
        {
            return !string.IsNullOrWhiteSpace(targetSource) &&
                   targetSource.StartsWith("Camera ray fallback", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsClashPointTarget(string targetSource)
        {
            return string.Equals(targetSource, "ClashPoint", StringComparison.OrdinalIgnoreCase);
        }

        private static XYZ ToInternalPointMeters(double x, double y, double z)
        {
            return new XYZ(
                UnitUtils.ConvertToInternalUnits(x, UnitTypeId.Meters),
                UnitUtils.ConvertToInternalUnits(y, UnitTypeId.Meters),
                UnitUtils.ConvertToInternalUnits(z, UnitTypeId.Meters));
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static bool ApplySectionBoxAroundElements(View3D view3d, IList<MatchedElement> matchedElements)
        {
            BoundingBoxXYZ elementBox = CreateSectionBoxAroundElements(matchedElements);
            if (elementBox == null)
            {
                return false;
            }

            using (Transaction t = new Transaction(view3d.Document, "Apply Element Section Box"))
            {
                t.Start();
                view3d.SetSectionBox(elementBox);
                view3d.IsSectionBoxActive = true;
                t.Commit();
            }

            return true;
        }

        private static void AddMatchedElement(List<MatchedElement> matches, MatchedElement match)
        {
            if (matches == null || match == null || match.Element == null) return;

            foreach (MatchedElement existing in matches)
            {
                if (IsSameMatchedElement(existing, match))
                {
                    return;
                }
            }

            matches.Add(match);
        }

        private static bool IsSameMatchedElement(MatchedElement first, MatchedElement second)
        {
            if (first == null || second == null) return false;
            if (!ElementIdsEqual(first.Element?.Id, second.Element?.Id)) return false;

            ElementId firstLinkId = first.LinkInstance?.Id;
            ElementId secondLinkId = second.LinkInstance?.Id;
            if (firstLinkId == null && secondLinkId == null) return true;
            return ElementIdsEqual(firstLinkId, secondLinkId);
        }

        private static List<ElementId> GetVisibilityElementIds(IList<MatchedElement> matchedElements)
        {
            List<ElementId> ids = new List<ElementId>();
            if (matchedElements == null) return ids;

            foreach (MatchedElement match in matchedElements)
            {
                ElementId id = match.IsLinked ? match.LinkInstance.Id : match.Element.Id;
                AddElementId(ids, id);
            }

            return ids;
        }

        private static List<ElementId> GetLinkedInstanceIds(IList<MatchedElement> matchedElements)
        {
            List<ElementId> ids = new List<ElementId>();
            if (matchedElements == null) return ids;

            foreach (MatchedElement match in matchedElements)
            {
                if (match.IsLinked)
                {
                    AddElementId(ids, match.LinkInstance.Id);
                }
            }

            return ids;
        }

        private static void AddElementId(List<ElementId> ids, ElementId id)
        {
            if (ids == null || id == null || ElementIdsEqual(id, ElementId.InvalidElementId)) return;

            foreach (ElementId existing in ids)
            {
                if (ElementIdsEqual(existing, id))
                {
                    return;
                }
            }

            ids.Add(id);
        }

        private static bool ElementIdsEqual(ElementId first, ElementId second)
        {
            if (first == null || second == null) return first == null && second == null;
            return first.Equals(second);
        }

        private static bool ZoomToSectionBox(UIDocument uidoc, View3D view3d)
        {
            return ZoomToSectionBox(uidoc, view3d, SectionBoxViewportZoomFactor);
        }

        private static bool ZoomToSectionBox(UIDocument uidoc, View3D view3d, double viewportZoomFactor)
        {
            UIView uiView = uidoc.GetOpenUIViews().FirstOrDefault(v => v.ViewId == view3d.Id);
            if (uiView == null)
            {
                return false;
            }

            BoundingBoxXYZ sectionBox = view3d.GetSectionBox();
            if (sectionBox == null)
            {
                return false;
            }

            GetWorldExtents(sectionBox, out XYZ min, out XYZ max);
            ShrinkRectangleAroundCenter(min, max, viewportZoomFactor, out XYZ zoomMin, out XYZ zoomMax);
            uiView.ZoomAndCenterRectangle(zoomMin, zoomMax);
            AppendSyncLog(
                $"ZoomToSectionBox: viewportFactor={viewportZoomFactor:F2}, " +
                $"sectionMin=({min.X:F1},{min.Y:F1},{min.Z:F1})ft, sectionMax=({max.X:F1},{max.Y:F1},{max.Z:F1})ft, " +
                $"zoomMin=({zoomMin.X:F1},{zoomMin.Y:F1},{zoomMin.Z:F1})ft, zoomMax=({zoomMax.X:F1},{zoomMax.Y:F1},{zoomMax.Z:F1})ft");
            return true;
        }

        private static void ShrinkRectangleAroundCenter(
            XYZ min,
            XYZ max,
            double factor,
            out XYZ zoomMin,
            out XYZ zoomMax)
        {
            factor = Clamp(factor, 0.25, 1.0);
            XYZ center = (min + max) * 0.5;
            XYZ half = (max - min) * (factor * 0.5);
            const double minHalfExtent = 1.0;

            half = new XYZ(
                Math.Max(Math.Abs(half.X), minHalfExtent),
                Math.Max(Math.Abs(half.Y), minHalfExtent),
                Math.Max(Math.Abs(half.Z), minHalfExtent));

            zoomMin = center - half;
            zoomMax = center + half;
        }

        private static void ExportViewCompareSnapshot(Document doc, View3D view3d, ViewpointModel viewpoint)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string exportDir = Path.Combine(localAppData, "AntigravityIssueManager", "ViewCompare");
                Directory.CreateDirectory(exportDir);

                string prefix = $"revit_view_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}";
                string exportBasePath = Path.Combine(exportDir, prefix);
                string normalizedPath = Path.Combine(exportDir, prefix + ".png");
                DateTime exportStartUtc = DateTime.UtcNow;

                ImageExportOptions options = new ImageExportOptions
                {
                    ExportRange = ExportRange.VisibleRegionOfCurrentView,
                    FilePath = exportBasePath,
                    HLRandWFViewsFileType = ImageFileType.PNG,
                    ShadowViewsFileType = ImageFileType.PNG,
                    ImageResolution = ImageResolution.DPI_72,
                    ZoomType = ZoomFitType.FitToPage,
                    PixelSize = 1400,
                    ShouldCreateWebSite = false
                };

                doc.ExportImage(options);

                string[] exportedFiles = Directory.GetFiles(exportDir, "*.png")
                    .Where(f => File.GetLastWriteTimeUtc(f) >= exportStartUtc.AddSeconds(-2))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .ToArray();

                if (exportedFiles.Length > 0)
                {
                    string sourcePath = exportedFiles[0];
                    if (!string.Equals(sourcePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(normalizedPath))
                        {
                            File.Delete(normalizedPath);
                        }

                        File.Move(sourcePath, normalizedPath);
                    }

                    foreach (string file in exportedFiles.Where(f => !string.Equals(f, normalizedPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        try { File.Delete(file); } catch { }
                    }

                    AppendSyncLog(
                        $"View compare snapshot exported:{Environment.NewLine}" +
                        $"BCF snapshot: {viewpoint?.SnapshotFilePath ?? "(none)"}{Environment.NewLine}" +
                        $"Revit view: {normalizedPath}");
                    return;
                }

                AppendSyncLog("View compare snapshot export produced no PNG.");
            }
            catch (Exception ex)
            {
                AppendSyncLog("View compare snapshot export failed: " + ex.Message);
            }
        }

        private static void GetWorldExtents(BoundingBoxXYZ box, out XYZ min, out XYZ max)
        {
            Transform transform = box.Transform ?? Transform.Identity;
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

            XYZ first = transform.OfPoint(corners[0]);
            double minX = first.X;
            double minY = first.Y;
            double minZ = first.Z;
            double maxX = first.X;
            double maxY = first.Y;
            double maxZ = first.Z;

            foreach (XYZ corner in corners.Skip(1).Select(transform.OfPoint))
            {
                minX = Math.Min(minX, corner.X);
                minY = Math.Min(minY, corner.Y);
                minZ = Math.Min(minZ, corner.Z);
                maxX = Math.Max(maxX, corner.X);
                maxY = Math.Max(maxY, corner.Y);
                maxZ = Math.Max(maxZ, corner.Z);
            }

            min = new XYZ(minX, minY, minZ);
            max = new XYZ(maxX, maxY, maxZ);
        }

        private static BoundingBoxXYZ CreateSectionBoxAroundElements(IList<MatchedElement> matchedElements)
        {
            BoundingBoxXYZ merged = CreateMergedElementBoundingBox(matchedElements);
            if (merged == null)
            {
                return null;
            }

            const double paddingFeet = 15.0;
            return new BoundingBoxXYZ
            {
                Transform = Transform.Identity,
                Min = new XYZ(merged.Min.X - paddingFeet, merged.Min.Y - paddingFeet, merged.Min.Z - paddingFeet),
                Max = new XYZ(merged.Max.X + paddingFeet, merged.Max.Y + paddingFeet, merged.Max.Z + paddingFeet)
            };
        }

        private static BoundingBoxXYZ CreateMergedElementBoundingBox(IList<MatchedElement> matchedElements)
        {
            if (matchedElements == null || matchedElements.Count == 0)
            {
                return null;
            }

            bool hasBox = false;
            double minX = 0;
            double minY = 0;
            double minZ = 0;
            double maxX = 0;
            double maxY = 0;
            double maxZ = 0;

            foreach (MatchedElement match in matchedElements)
            {
                BoundingBoxXYZ box = match.GetTransformedBoundingBox();
                if (box == null)
                {
                    continue;
                }

                if (!hasBox)
                {
                    minX = box.Min.X;
                    minY = box.Min.Y;
                    minZ = box.Min.Z;
                    maxX = box.Max.X;
                    maxY = box.Max.Y;
                    maxZ = box.Max.Z;
                    hasBox = true;
                    continue;
                }

                minX = Math.Min(minX, box.Min.X);
                minY = Math.Min(minY, box.Min.Y);
                minZ = Math.Min(minZ, box.Min.Z);
                maxX = Math.Max(maxX, box.Max.X);
                maxY = Math.Max(maxY, box.Max.Y);
                maxZ = Math.Max(maxZ, box.Max.Z);
            }

            if (!hasBox)
            {
                return null;
            }

            return new BoundingBoxXYZ
            {
                Transform = Transform.Identity,
                Min = new XYZ(minX, minY, minZ),
                Max = new XYZ(maxX, maxY, maxZ)
            };
        }

        private static View3D GetOrCreateBcfView(Document doc)
        {
            const string viewName = "BCF Issue View";
            int cacheKey = GetDocumentCacheKey(doc);
            if (BcfViewCache.TryGetValue(cacheKey, out ElementId cachedViewId) &&
                doc.GetElement(cachedViewId) is View3D cachedView &&
                !cachedView.IsTemplate &&
                !cachedView.IsPerspective &&
                cachedView.Name == viewName)
            {
                return cachedView;
            }

            View3D legacyPerspectiveView = null;
            
            FilteredElementCollector viewCollector = new FilteredElementCollector(doc).OfClass(typeof(View3D));
            foreach (View3D v in viewCollector)
            {
                if (v.Name != viewName || v.IsTemplate)
                {
                    continue;
                }

                if (!v.IsPerspective)
                {
                    BcfViewCache[cacheKey] = v.Id;
                    return v;
                }

                legacyPerspectiveView = v;
            }

            ViewFamilyType viewFamilyType = null;
            FilteredElementCollector vftCollector = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType));
            foreach (ViewFamilyType vft in vftCollector)
            {
                if (vft.ViewFamily == ViewFamily.ThreeDimensional)
                {
                    viewFamilyType = vft;
                    break;
                }
            }

            if (viewFamilyType == null) return null;

            View3D newView = null;
            using (Transaction t = new Transaction(doc, "Create BCF Issue View"))
            {
                t.Start();
                if (legacyPerspectiveView != null)
                {
                    legacyPerspectiveView.Name = GetUniqueViewName(doc, "BCF Issue View - Perspective Legacy");
                }

                newView = View3D.CreateIsometric(doc, viewFamilyType.Id);
                newView.Name = viewName;
                newView.DetailLevel = ViewDetailLevel.Fine;
                newView.DisplayStyle = DisplayStyle.Realistic;
                t.Commit();
            }

            if (newView != null)
            {
                BcfViewCache[cacheKey] = newView.Id;
            }

            return newView;
        }

        private static string GetUniqueViewName(Document doc, string baseName)
        {
            HashSet<string> viewNames = new HashSet<string>(
                new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate)
                .Select(v => v.Name),
                StringComparer.OrdinalIgnoreCase);

            if (!viewNames.Contains(baseName))
            {
                return baseName;
            }

            for (int i = 1; i < 1000; i++)
            {
                string candidate = $"{baseName} {i}";
                if (!viewNames.Contains(candidate))
                {
                    return candidate;
                }
            }

            return $"{baseName} {Guid.NewGuid():N}";
        }

        private static MatchedElement FindElementByStringIdentifier(
            Document hostDoc,
            string identifier,
            IDictionary<IFCGuidKey, ElementId> ifcGuidMap)
        {
            Element hostElement = FindElementInDoc(hostDoc, identifier, ifcGuidMap);
            if (hostElement != null)
            {
                return new MatchedElement(hostElement, null);
            }

            FilteredElementCollector linkCollector = new FilteredElementCollector(hostDoc).OfClass(typeof(RevitLinkInstance));
            foreach (RevitLinkInstance linkInstance in linkCollector)
            {
                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null)
                {
                    continue;
                }

                Element linkElement = FindElementInDoc(linkDoc, identifier, null);
                if (linkElement != null)
                {
                    return new MatchedElement(linkElement, linkInstance);
                }
            }

            return null;
        }

        private static List<string> GetElementIdentifiers(ViewpointModel viewpoint)
        {
            List<string> identifiers = new List<string>();
            if (viewpoint?.ElementIds != null)
            {
                foreach (string elementId in viewpoint.ElementIds)
                {
                    AddIdentifier(identifiers, elementId);
                    if (!string.IsNullOrWhiteSpace(elementId) &&
                        viewpoint.ComponentIfcGuids != null &&
                        viewpoint.ComponentIfcGuids.TryGetValue(elementId.Trim(), out string ifcGuid))
                    {
                        AddIdentifier(identifiers, ifcGuid);
                    }
                }
            }

            if (viewpoint?.ComponentIfcGuids != null)
            {
                foreach (string ifcGuid in viewpoint.ComponentIfcGuids.Values)
                {
                    AddIdentifier(identifiers, ifcGuid);
                }
            }

            return identifiers;
        }

        private static void AddIdentifier(List<string> identifiers, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string trimmed = value.Trim();
            if (!identifiers.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                identifiers.Add(trimmed);
            }
        }

        private static Element FindElementInDoc(
            Document doc,
            string identifier,
            IDictionary<IFCGuidKey, ElementId> ifcGuidMap)
        {
            if (string.IsNullOrWhiteSpace(identifier) || doc == null) return null;

            string value = identifier.Trim();
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long idLong))
            {
                Element byId = doc.GetElement(new ElementId(idLong));
                if (byId != null)
                {
                    return byId;
                }
            }

            try
            {
                Element byUniqueId = doc.GetElement(value);
                if (byUniqueId != null)
                {
                    return byUniqueId;
                }
            }
            catch
            {
                // Not a Revit UniqueId.
            }

            bool isLikelyIfcGuid = IsLikelyIfcGuid(value);
            if (ifcGuidMap != null && isLikelyIfcGuid)
            {
                try
                {
                    IFCGuidKey ifcGuidKey = new IFCGuidKey(value);
                    if (ifcGuidMap.TryGetValue(ifcGuidKey, out ElementId mappedId))
                    {
                        return doc.GetElement(mappedId);
                    }
                }
                catch
                {
                    // Not a valid IFC GUID key.
                }
            }

            if (isLikelyIfcGuid && EnableExpensiveIfcParameterFallback)
            {
                IDictionary<string, ElementId> parameterMap = GetOrCreateIfcParameterMap(doc);
                if (parameterMap.TryGetValue(value, out ElementId parameterMappedId))
                {
                    return doc.GetElement(parameterMappedId);
                }
            }

            return null;
        }

        private static bool NeedsIfcGuidMap(IEnumerable<string> identifiers)
        {
            return identifiers != null && identifiers.Any(IsLikelyIfcGuid);
        }

        private static IDictionary<IFCGuidKey, ElementId> GetOrCreateIfcGuidMap(Document doc)
        {
            int key = GetDocumentCacheKey(doc);
            if (IfcGuidMapCache.TryGetValue(key, out IDictionary<IFCGuidKey, ElementId> cachedMap))
            {
                return cachedMap;
            }

            IDictionary<IFCGuidKey, ElementId> map = CreateIfcGuidMap(doc);
            IfcGuidMapCache[key] = map;
            return map;
        }

        private static IDictionary<IFCGuidKey, ElementId> CreateIfcGuidMap(Document doc)
        {
            try
            {
                IList<ElementId> candidateIds = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .ToElementIds()
                    .ToList();

                if (candidateIds == null || candidateIds.Count == 0)
                {
                    return null;
                }

                return new IFCHybridImport().CreateMap(doc, candidateIds);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("IFC GUID map error: " + ex.Message);
                return null;
            }
        }

        private static IDictionary<string, ElementId> GetOrCreateIfcParameterMap(Document doc)
        {
            int key = GetDocumentCacheKey(doc);
            if (IfcParameterMapCache.TryGetValue(key, out IDictionary<string, ElementId> cachedMap))
            {
                return cachedMap;
            }

            Dictionary<string, ElementId> map = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);
            Stopwatch stopwatch = Stopwatch.StartNew();
            int scanned = 0;

            foreach (Element element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
            {
                scanned++;
                AddIfcParameterValue(map, element, "IFC GUID");
                AddIfcParameterValue(map, element, "IfcGUID");
                AddIfcParameterValue(map, element, "IfcGuid");
                AddIfcParameterValue(map, element, "IfcGlobalId");
                AddIfcParameterValue(map, element, "GlobalId");
            }

            stopwatch.Stop();
            AppendSyncLog(
                $"IFC parameter map built: doc='{doc.Title}', scanned={scanned}, mapped={map.Count}, elapsed={stopwatch.ElapsedMilliseconds}ms");

            IfcParameterMapCache[key] = map;
            return map;
        }

        private static void AddIfcParameterValue(Dictionary<string, ElementId> map, Element element, string parameterName)
        {
            Parameter parameter = element.LookupParameter(parameterName);
            string value = parameter?.AsString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string trimmed = value.Trim();
            if (IsLikelyIfcGuid(trimmed) && !map.ContainsKey(trimmed))
            {
                map[trimmed] = element.Id;
            }
        }

        private static bool IsLikelyIfcGuid(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            if (trimmed.Length != 22)
            {
                return false;
            }

            const string ifcGuidAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";
            return trimmed.All(c => ifcGuidAlphabet.IndexOf(c) >= 0);
        }

        private static int GetDocumentCacheKey(Document doc)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (doc?.GetHashCode() ?? 0);
                hash = hash * 31 + (doc?.PathName ?? string.Empty).GetHashCode();
                return hash;
            }
        }

        private class MatchedElement
        {
            public MatchedElement(Element element, RevitLinkInstance linkInstance)
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
                if (box == null)
                {
                    return null;
                }

                Transform boxTransform = box.Transform ?? Transform.Identity;
                Transform linkTransform = IsLinked
                    ? LinkInstance.GetTotalTransform()
                    : Transform.Identity;

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

        private class CoordinateConversion
        {
            public string ModeName { get; set; }
            public XYZ Camera { get; set; }
            public XYZ Target { get; set; }
            public XYZ Forward { get; set; }
            public XYZ Up { get; set; }
            public List<ConvertedClippingPlane> ClippingPlanes { get; set; } = new List<ConvertedClippingPlane>();
            public string TargetSource { get; set; }
            public BoundingBoxXYZ ModelBox { get; set; }
            public double DistanceToModel { get; set; }
            public double SelectionScore { get; set; }
        }

        private class ConvertedClippingPlane
        {
            public XYZ Location { get; set; }
            public XYZ Direction { get; set; }
        }

        private class ClipPlanePair
        {
            public XYZ Axis { get; set; }
            public double CenterOffset { get; set; }
            public double HalfExtent { get; set; }
        }

        private class SyncTiming
        {
            private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
            private readonly List<string> _steps = new List<string>();
            private long _lastMilliseconds;

            public void Mark(string step)
            {
                long elapsed = _stopwatch.ElapsedMilliseconds;
                long delta = elapsed - _lastMilliseconds;
                _lastMilliseconds = elapsed;
                _steps.Add($"{elapsed,6} ms (+{delta,5}): {step}");
            }

            public string ToLogString()
            {
                _stopwatch.Stop();
                Mark("finish");
                return "=== BCF Show in Model Timing ===" + Environment.NewLine +
                       string.Join(Environment.NewLine, _steps);
            }
        }
    }
}
