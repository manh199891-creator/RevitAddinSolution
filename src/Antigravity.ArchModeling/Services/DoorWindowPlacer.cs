using Autodesk.Revit.DB;
using Antigravity.ArchModeling.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.ArchModeling.Services
{
    public class DoorPlacementResult
    {
        public FamilyInstance Instance { get; set; }
        public bool IsAmbiguous { get; set; }
        public string EntityHandle { get; set; }
    }

    public class DoorWindowPlacer
    {
        private static readonly XYZ BaseFacing  = new XYZ(1, 0, 0);
        private static readonly XYZ BaseHanding = new XYZ(0, -1, 0);

        public DoorPlacementResult PlaceDoorOrWindow(
            Document doc,
            BlockInfo block,
            Dictionary<string, FamilySymbol> blockToSymbolMap,
            Level targetLevel,
            bool mirrorHinge = false)
        {
            if (!blockToSymbolMap.TryGetValue(block.BlockName, out FamilySymbol symbol))
                return null;

            if (!symbol.IsActive) symbol.Activate();

            XYZ location = Antigravity.Core.Services.CoordinateService.CadToRevit(block.X, block.Y, 0);
            location = new XYZ(location.X, location.Y, targetLevel.Elevation);

            Wall hostWall = FindClosestWall(doc, location, targetLevel);

            FamilyInstance instance = null;
            if (hostWall != null)
            {
                instance = doc.Create.NewFamilyInstance(
                    location, symbol, hostWall, targetLevel,
                    Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            }
            else
            {
                try
                {
                    instance = doc.Create.NewFamilyInstance(
                        location, symbol, targetLevel,
                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                }
                catch { return null; }
            }

            if (instance == null)
                return new DoorPlacementResult { Instance = null, EntityHandle = block.EntityHandle };

            // ═══ CRITICAL: Regenerate so FacingOrientation/HandOrientation are valid ═══
            doc.Regenerate();

            bool isAmbiguous = false;

            if (hostWall != null)
            {
                XYZ wallOrientation = hostWall.Orientation;
                XYZ wallDir = XYZ.BasisZ.CrossProduct(wallOrientation).Normalize();

                // ═══ STEP 1: Try Internal ARC Geometry (most accurate) ═══
                int cadFacingSign = 0;
                int cadHandingSign = 0;
                double threshold = 10.0 / 304.8; // 10mm

                if (Math.Abs(block.FacingVectorX) > 0.001 || Math.Abs(block.FacingVectorY) > 0.001)
                {
                    XYZ internalVec = new XYZ(block.FacingVectorX / 304.8, block.FacingVectorY / 304.8, 0);
                    
                    double internalFacingProj = internalVec.DotProduct(wallOrientation);
                    double internalHandingProj = internalVec.DotProduct(wallDir);
                    
                    cadFacingSign = Math.Abs(internalFacingProj) > threshold ? Math.Sign(internalFacingProj) : 0;
                    // Usually facing is enough from arc, but let's check handing too just in case
                    cadHandingSign = Math.Abs(internalHandingProj) > threshold ? Math.Sign(internalHandingProj) : 0;
                }

                // ═══ STEP 2: Fallback to BBox geometry (works for asymmetric doors) ═══
                if (cadFacingSign == 0)
                {
                    double dx = block.CenterX - block.X;
                    double dy = block.CenterY - block.Y;
                    XYZ swingVector = new XYZ(dx / 304.8, dy / 304.8, 0);

                    double facingProj  = swingVector.DotProduct(wallOrientation);
                    double handingProj = swingVector.DotProduct(wallDir);

                    if (cadFacingSign == 0)
                        cadFacingSign = Math.Abs(facingProj) > threshold ? Math.Sign(facingProj) : 0;
                    if (cadHandingSign == 0)
                        cadHandingSign = Math.Abs(handingProj) > threshold ? Math.Sign(handingProj) : 0;
                }

                // ═══ STEP 3: Fallback to rotation for symmetric/centered doors ═══
                if (cadFacingSign == 0 || cadHandingSign == 0)
                {
                    double cos = Math.Cos(block.RotationRad);
                    double sin = Math.Sin(block.RotationRad);
                    double sx = (block.ScaleX < 0 ? -1.0 : 1.0) * (block.IsFlippedX ? -1.0 : 1.0);
                    double sy = (block.ScaleY < 0 ? -1.0 : 1.0) * (block.IsFlippedY ? -1.0 : 1.0);

                    XYZ blockXInWorld = new XYZ(sx * cos, sx * sin, 0).Normalize();
                    XYZ blockYInWorld = new XYZ(-sy * sin, sy * cos, 0).Normalize();

                    if (cadFacingSign == 0)
                        cadFacingSign = Math.Sign(blockXInWorld.DotProduct(wallOrientation));
                    if (cadHandingSign == 0)
                        cadHandingSign = Math.Sign(blockYInWorld.DotProduct(wallDir));
                }

                if (cadFacingSign == 0 && cadHandingSign == 0)
                {
                    isAmbiguous = true;
                }

                // ═══ FACING ═══
                if (cadFacingSign != 0)
                {
                    int revitFacingSign = Math.Sign(instance.FacingOrientation.DotProduct(wallOrientation));
                    // REVERSED LOGIC per user request
                    if (cadFacingSign != revitFacingSign)
                    {
                        try { instance.flipFacing(); } catch { }
                    }
                }

                // ═══ HANDING ═══
                if (cadHandingSign != 0)
                {
                    int revitHandingSign = Math.Sign(instance.HandOrientation.DotProduct(wallDir));
                    // REVERSED LOGIC per user request
                    if (cadHandingSign == revitHandingSign)
                    {
                        try { instance.flipHand(); } catch { }
                    }
                }
            }
            else if (instance.Location is LocationPoint locPt)
            {
                Line axis = Line.CreateBound(location, location + XYZ.BasisZ);
                try { locPt.Rotate(axis, block.RotationRad); } catch { }
            }

            // ═══ MIRROR HINGE: flip hand one extra time for single-leaf doors ═══
            if (mirrorHinge && instance != null)
            {
                try { instance.flipHand(); } catch { }
            }

            return new DoorPlacementResult
            {
                Instance     = instance,
                IsAmbiguous  = isAmbiguous,
                EntityHandle = block.EntityHandle,
            };
        }

        private Wall FindClosestWall(Document doc, XYZ pt, Level level)
        {
            var walls = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .Where(w => w.LevelId == level.Id);

            Wall closest  = null;
            double minDist = double.MaxValue;

            foreach (var w in walls)
            {
                if (!(w.Location is LocationCurve lc)) continue;

                IntersectionResult res = lc.Curve.Project(
                    new XYZ(pt.X, pt.Y, lc.Curve.GetEndPoint(0).Z));

                double d = res != null
                    ? res.Distance
                    : Math.Min(pt.DistanceTo(lc.Curve.GetEndPoint(0)),
                               pt.DistanceTo(lc.Curve.GetEndPoint(1)));

                if (d < minDist) { minDist = d; closest = w; }
            }

            return minDist < 5.0 ? closest : null;
        }
    }
}
