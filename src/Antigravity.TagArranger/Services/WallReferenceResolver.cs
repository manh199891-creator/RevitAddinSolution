using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Antigravity.TagArranger.Services
{
    public static class WallReferenceResolver
    {
        // ----------------------------------------------------------------
        // APPROACH A — HostObjectUtils (nhanh, Revit 2024 ổn)
        // Trả null nếu không lấy được (bug hoặc curtain wall)
        // ----------------------------------------------------------------
        public static Reference GetFaceRef_HostObjectUtils(
            Wall wall,
            bool exterior)  // true = exterior face, false = interior face
        {
            try
            {
                var shellLayer = exterior
                    ? ShellLayerType.Exterior
                    : ShellLayerType.Interior;

                var faces = HostObjectUtils.GetSideFaces(wall, shellLayer);
                return (faces != null && faces.Count > 0) ? faces[0] : null;
            }
            catch { return null; }
        }

        // ----------------------------------------------------------------
        // APPROACH B — Geometry traversal: tìm face gần nhất với hướng dim
        // Fallback khi HostObjectUtils không work
        // ----------------------------------------------------------------
        public static Reference GetFaceRef_Geometry(
            Wall wall,
            XYZ faceNormalDirection,
            View activeView)   // hướng pháp tuyến của face muốn tìm
        {
            var opts = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine,
                View = activeView
            };

            var geom = wall.get_Geometry(opts);
            if (geom == null) return null;

            Reference bestRef = null;
            double bestDot = -1;

            foreach (var obj in geom)
            {
                Solid solid = null;

                if (obj is Solid s && s.Volume > 1e-9)
                    solid = s;
                else if (obj is GeometryInstance inst)
                {
                    foreach (var o2 in inst.GetInstanceGeometry())
                        if (o2 is Solid s2 && s2.Volume > 1e-9) { solid = s2; break; }
                }

                if (solid == null) continue;

                foreach (Face face in solid.Faces)
                {
                    if (face.Reference == null) continue;

                    // Lấy normal tại điểm giữa face (centroid approximate)
                    var bbox = face.GetBoundingBox();
                    var uvMid = new UV(
                        (bbox.Min.U + bbox.Max.U) / 2,
                        (bbox.Min.V + bbox.Max.V) / 2);
                    
                    XYZ normal = null;
                    try { normal = face.ComputeNormal(uvMid); } catch { continue; }

                    // Face có normal gần nhất với hướng mong muốn
                    double dot = normal.DotProduct(faceNormalDirection);
                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        bestRef = face.Reference;
                    }
                }
            }

            // Chỉ trả về nếu dot > 0.7 (~cos45°) — tức là face tương đối vuông góc đúng
            return (bestDot > 0.7) ? bestRef : null;
        }

        // ----------------------------------------------------------------
        // PUBLIC API — tự động thử A rồi fallback B
        // ----------------------------------------------------------------
        public static Reference GetWallFaceReference(
            Wall wall,
            View activeView,
            bool wantExterior = true)
        {
            // Thử HostObjectUtils trước
            var refA = GetFaceRef_HostObjectUtils(wall, wantExterior);
            if (refA != null) return refA;

            // Fallback: geometry traversal
            var wallNormal = GetWallNormal(wall);
            var dir = wantExterior ? wallNormal : wallNormal.Negate();
            return GetFaceRef_Geometry(wall, dir, activeView);
        }

        // ----------------------------------------------------------------
        // Tính wall normal direction từ LocationCurve
        // ----------------------------------------------------------------
        public static XYZ GetWallNormal(Wall wall)
        {
            var lc = wall.Location as LocationCurve;
            if (lc?.Curve == null) return XYZ.BasisX;

            var p0 = lc.Curve.GetEndPoint(0);
            var p1 = lc.Curve.GetEndPoint(1);
            var dir = (p1 - p0).Normalize();

            // Normal = perpendicular trong mặt phẳng XY (floor plan)
            return new XYZ(-dir.Y, dir.X, 0);
        }
    }
}
