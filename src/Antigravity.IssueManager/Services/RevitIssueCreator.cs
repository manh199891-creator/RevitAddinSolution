using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.IssueManager.Models;

namespace Antigravity.IssueManager.Services
{
    public static class RevitIssueCreator
    {
        public static IssueModel CreateIssueFromCurrentView(UIApplication uiApp, string title, string description)
        {
            return CreateIssueFromCurrentView(uiApp, title, description, true, false);
        }

        public static IssueModel CreateIssueFromCurrentView(
            UIApplication uiApp,
            string title,
            string description,
            bool includeSectionBoxClipPlanes)
        {
            return CreateIssueFromCurrentView(uiApp, title, description, includeSectionBoxClipPlanes, false);
        }

        public static IssueModel CreateIssueFromCurrentView(
            UIApplication uiApp,
            string title,
            string description,
            bool includeSectionBoxClipPlanes,
            bool useSharedCoordinates,
            string image2DPath = null,
            string level = null,
            string assignedTo = null)
        {
            if (uiApp == null) throw new ArgumentNullException(nameof(uiApp));

            UIDocument uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null) throw new InvalidOperationException("No active Revit document.");

            Document doc = uiDoc.Document;
            if (doc.ActiveView.ViewType != ViewType.ThreeD || !(doc.ActiveView is View3D view3d))
            {
                throw new InvalidOperationException("Create Issue only supports 3D views.");
            }

            UIView uiView = GetActiveUIView(uiDoc, view3d);
            ProjectPosition projectPosition = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
            XYZ cameraForward = GetCameraForwardDirection(view3d);

            bool isOrthogonal = !view3d.IsPerspective;
            XYZ cameraPoint = isOrthogonal
                ? ToBcfPoint(GetOrthogonalCameraEye(uiView, cameraForward), projectPosition, useSharedCoordinates)
                : ToBcfPoint(GetPerspectiveCameraEye(view3d), projectPosition, useSharedCoordinates);
            XYZ cameraDirection = ToBcfVector(cameraForward, projectPosition, useSharedCoordinates);
            XYZ cameraUp = ToBcfVector(GetCameraUpDirection(view3d), projectPosition, useSharedCoordinates);
            double viewToWorldScale = isOrthogonal ? GetViewToWorldScale(uiView, view3d) : 0;
            List<ClippingPlaneModel> clippingPlanes = includeSectionBoxClipPlanes
                ? CreateSectionBoxClipPlanes(view3d, projectPosition, useSharedCoordinates)
                : new List<ClippingPlaneModel>();

            List<string> selectedElementIds = new List<string>();
            Dictionary<string, string> componentIfcGuids = new Dictionary<string, string>();

            foreach (ElementId id in uiDoc.Selection.GetElementIds())
            {
                selectedElementIds.Add(id.ToString());
                try
                {
                    Guid exportGuid = ExportUtils.GetExportId(doc, id);
                    componentIfcGuids[id.ToString()] = BcfExporter.ToIfcGuid(exportGuid);
                }
                catch
                {
                    // Fallback if anything goes wrong
                }
            }

            string snapshotPath = ExportCurrentViewSnapshot(doc);

            return new IssueModel
            {
                IssueId = Guid.NewGuid().ToString().ToUpperInvariant(),
                Title = title,
                Level = level ?? string.Empty,
                AssignedTo = assignedTo ?? string.Empty,
                Description = description ?? string.Empty,
                Status = "Active",
                Author = Environment.UserName,
                CreationDate = DateTime.Now,
                Distance = string.Empty,
                Viewpoint = new ViewpointModel
                {
                    CoordinateMode = useSharedCoordinates ? "Shared" : "Internal",
                    CameraX = cameraPoint.X,
                    CameraY = cameraPoint.Y,
                    CameraZ = cameraPoint.Z,
                    CameraDirectionX = cameraDirection.X,
                    CameraDirectionY = cameraDirection.Y,
                    CameraDirectionZ = cameraDirection.Z,
                    CameraUpX = cameraUp.X,
                    CameraUpY = cameraUp.Y,
                    CameraUpZ = cameraUp.Z,
                    IsOrthogonal = isOrthogonal,
                    ViewToWorldScale = viewToWorldScale,
                    ElementIds = selectedElementIds,
                    ComponentIfcGuids = componentIfcGuids,
                    ClippingPlanes = clippingPlanes,
                    SnapshotFilePath = snapshotPath,
                    SnapshotFilePath2 = image2DPath
                }
            };
        }

        private static UIView GetActiveUIView(UIDocument uiDoc, View3D view3d)
        {
            UIView uiView = uiDoc.GetOpenUIViews().FirstOrDefault(v => v.ViewId == view3d.Id);
            if (uiView == null)
            {
                throw new InvalidOperationException("Could not find the active Revit UI view.");
            }

            return uiView;
        }

        private static XYZ GetPerspectiveCameraEye(View3D view3d)
        {
            try
            {
                ViewOrientation3D orientation = view3d.GetOrientation();
                if (orientation?.EyePosition != null)
                {
                    return orientation.EyePosition;
                }
            }
            catch
            {
                // Fall back to the legacy value below if Revit cannot provide the UI camera orientation.
            }

            return view3d.Origin;
        }

        private static XYZ GetCameraForwardDirection(View3D view3d)
        {
            try
            {
                ViewOrientation3D orientation = view3d.GetOrientation();
                if (orientation?.ForwardDirection != null && !orientation.ForwardDirection.IsZeroLength())
                {
                    return orientation.ForwardDirection.Normalize();
                }
            }
            catch
            {
                // Fall back to ViewDirection below.
            }

            if (view3d.ViewDirection != null && !view3d.ViewDirection.IsZeroLength())
            {
                return view3d.ViewDirection.Negate().Normalize();
            }

            return XYZ.BasisY;
        }

        private static XYZ GetCameraUpDirection(View3D view3d)
        {
            try
            {
                ViewOrientation3D orientation = view3d.GetOrientation();
                if (orientation?.UpDirection != null && !orientation.UpDirection.IsZeroLength())
                {
                    return orientation.UpDirection.Normalize();
                }
            }
            catch
            {
                // Fall back to View3D.UpDirection below.
            }

            if (view3d.UpDirection != null && !view3d.UpDirection.IsZeroLength())
            {
                return view3d.UpDirection.Normalize();
            }

            return XYZ.BasisZ;
        }

        private static XYZ GetOrthogonalCameraEye(UIView uiView, XYZ cameraForward)
        {
            try
            {
                IList<XYZ> corners = uiView.GetZoomCorners();
                if (corners == null || corners.Count < 2)
                {
                    throw new InvalidOperationException("Could not read the current 3D view zoom corners.");
                }

                XYZ center = (corners[0] + corners[1]) * 0.5;
                double diag = corners[0].DistanceTo(corners[1]);
                
                // BCF CameraViewPoint is the eye position; CameraDirection points from the eye into the model.
                XYZ eye = center - cameraForward.Normalize() * diag;
                return eye;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("View must be open and active to create orthogonal BCF.", ex);
            }
        }

        private static double GetViewToWorldScale(UIView uiView, View3D view3d)
        {
            IList<XYZ> corners = uiView.GetZoomCorners();
            if (corners == null || corners.Count < 2) return 0;

            XYZ span = corners[1] - corners[0];
            double heightFeet = Math.Abs(span.DotProduct(view3d.UpDirection));
            if (heightFeet <= 1e-9)
            {
                heightFeet = span.GetLength();
            }

            return UnitUtils.ConvertFromInternalUnits(heightFeet, UnitTypeId.Meters);
        }

        private static XYZ ToBcfPoint(XYZ internalPoint, ProjectPosition projectPosition, bool useSharedCoordinates)
        {
            XYZ bcfFeet = internalPoint;
            if (useSharedCoordinates)
            {
                Transform sharedRotation = Transform.CreateRotation(XYZ.BasisZ, projectPosition.Angle);
                bcfFeet = sharedRotation.OfPoint(internalPoint) + new XYZ(
                    projectPosition.EastWest,
                    projectPosition.NorthSouth,
                    projectPosition.Elevation);
            }

            return new XYZ(
                UnitUtils.ConvertFromInternalUnits(bcfFeet.X, UnitTypeId.Meters),
                UnitUtils.ConvertFromInternalUnits(bcfFeet.Y, UnitTypeId.Meters),
                UnitUtils.ConvertFromInternalUnits(bcfFeet.Z, UnitTypeId.Meters));
        }

        private static XYZ ToBcfVector(XYZ internalVector, ProjectPosition projectPosition, bool useSharedCoordinates)
        {
            if (internalVector == null || internalVector.IsZeroLength()) return XYZ.Zero;

            XYZ bcfVector = internalVector;
            if (useSharedCoordinates)
            {
                Transform sharedRotation = Transform.CreateRotation(XYZ.BasisZ, projectPosition.Angle);
                bcfVector = sharedRotation.OfVector(internalVector);
            }

            return bcfVector.Normalize();
        }

        private static List<ClippingPlaneModel> CreateSectionBoxClipPlanes(
            View3D view3d,
            ProjectPosition projectPosition,
            bool useSharedCoordinates)
        {
            List<ClippingPlaneModel> planes = new List<ClippingPlaneModel>();
            if (view3d == null || !view3d.IsSectionBoxActive)
            {
                return planes;
            }

            BoundingBoxXYZ bbox = view3d.GetSectionBox();
            if (bbox == null)
            {
                return planes;
            }

            XYZ min = bbox.Min;
            XYZ max = bbox.Max;
            XYZ mid = (min + max) * 0.5;
            Transform boxTransform = bbox.Transform ?? Transform.Identity;

            AddClipPlane(planes, boxTransform, projectPosition, useSharedCoordinates, new XYZ(min.X, mid.Y, mid.Z), XYZ.BasisX.Negate());
            AddClipPlane(planes, boxTransform, projectPosition, useSharedCoordinates, new XYZ(max.X, mid.Y, mid.Z), XYZ.BasisX);
            AddClipPlane(planes, boxTransform, projectPosition, useSharedCoordinates, new XYZ(mid.X, min.Y, mid.Z), XYZ.BasisY.Negate());
            AddClipPlane(planes, boxTransform, projectPosition, useSharedCoordinates, new XYZ(mid.X, max.Y, mid.Z), XYZ.BasisY);
            AddClipPlane(planes, boxTransform, projectPosition, useSharedCoordinates, new XYZ(mid.X, mid.Y, min.Z), XYZ.BasisZ.Negate());
            AddClipPlane(planes, boxTransform, projectPosition, useSharedCoordinates, new XYZ(mid.X, mid.Y, max.Z), XYZ.BasisZ);

            return planes;
        }

        private static void AddClipPlane(
            List<ClippingPlaneModel> planes,
            Transform boxTransform,
            ProjectPosition projectPosition,
            bool useSharedCoordinates,
            XYZ localLocation,
            XYZ localInwardDirection)
        {
            XYZ modelLocation = boxTransform.OfPoint(localLocation);
            XYZ modelDirection = boxTransform.OfVector(localInwardDirection);
            XYZ bcfLocation = ToBcfPoint(modelLocation, projectPosition, useSharedCoordinates);
            XYZ bcfDirection = ToBcfVector(modelDirection, projectPosition, useSharedCoordinates);

            planes.Add(new ClippingPlaneModel
            {
                LocationX = bcfLocation.X,
                LocationY = bcfLocation.Y,
                LocationZ = bcfLocation.Z,
                DirectionX = bcfDirection.X,
                DirectionY = bcfDirection.Y,
                DirectionZ = bcfDirection.Z
            });
        }

        public static string ExportCurrentViewSnapshot(Document doc)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "AntigravityIssueManager");
            Directory.CreateDirectory(tempDir);

            string prefix = "issue_" + Guid.NewGuid().ToString("N");
            string exportBasePath = Path.Combine(tempDir, prefix);
            string normalizedSnapshotPath = Path.Combine(tempDir, prefix + "_snapshot.png");

            ImageExportOptions options = new ImageExportOptions
            {
                ExportRange = ExportRange.VisibleRegionOfCurrentView,
                FilePath = exportBasePath,
                HLRandWFViewsFileType = ImageFileType.PNG,
                ShadowViewsFileType = ImageFileType.PNG,
                ImageResolution = ImageResolution.DPI_72,
                ZoomType = ZoomFitType.FitToPage,
                PixelSize = 1000,
                ShouldCreateWebSite = false
            };

            doc.ExportImage(options);

            string[] exportedFiles = Directory.GetFiles(tempDir, prefix + "*.png")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();

            if (exportedFiles.Length == 0)
            {
                throw new InvalidOperationException("Revit did not export a snapshot for the current view.");
            }

            if (File.Exists(normalizedSnapshotPath))
            {
                File.Delete(normalizedSnapshotPath);
            }

            File.Move(exportedFiles[0], normalizedSnapshotPath);
            
            // Clean up other temporary files exported by Revit
            foreach (var file in exportedFiles)
            {
                if (file != exportedFiles[0] && File.Exists(file))
                {
                    try { File.Delete(file); } catch { }
                }
            }

            return normalizedSnapshotPath;
        }
    }
}
