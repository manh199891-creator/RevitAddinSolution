using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Antigravity.WallMepClash.Models;

namespace Antigravity.WallMepClash.Services
{
    public class WallMepClashDetector
    {
        public List<ClashResult> RunCheck(
            Document hostDoc,
            IList<Wall> walls,
            RevitLinkInstance linkInstance,
            IList<Element> mepElements,
            ClashCheckSettings settings,
            Action<int, int> progressCallback = null)
        {
            var results = new List<ClashResult>();
            if (linkInstance == null || walls == null || mepElements == null)
                return results;

            Transform linkTransform = linkInstance.GetTotalTransform();
            Document linkDoc = linkInstance.GetLinkDocument();
            if (linkDoc == null)
                return results;

            int totalWalls = walls.Count;

            for (int i = 0; i < totalWalls; i++)
            {
                Wall wall = walls[i];
                progressCallback?.Invoke(i + 1, totalWalls);

                BoundingBoxXYZ wallBB = wall.get_BoundingBox(null);
                if (wallBB == null) continue;

                // Chiều dài tường bằng 0 hoặc tường trừu tượng (ví dụ room boundary) -> bỏ qua
                if (Math.Abs(wallBB.Max.X - wallBB.Min.X) < 0.001 && 
                    Math.Abs(wallBB.Max.Y - wallBB.Min.Y) < 0.001)
                    continue;

                XYZ wallDir = BoundingBoxHelper.GetWallDirection(wall);
                if (wallDir == null) continue;

                foreach (Element mep in mepElements)
                {
                    BoundingBoxXYZ mepBBLink = mep.get_BoundingBox(null);
                    if (mepBBLink == null) continue;

                    BoundingBoxXYZ mepBBHost = BoundingBoxHelper.TransformToHost(mepBBLink, linkTransform);

                    // 1. Kiểm tra va chạm Bounding Box (AABB Intersect)
                    if (!BoundingBoxHelper.Intersects(wallBB, mepBBHost)) continue;

                    // 2. Phân loại góc
                    XYZ mepDirLink = BoundingBoxHelper.GetElementDirection(mep);
                    if (mepDirLink == null)
                    {
                        // Point-based elements (ví dụ Electrical Fixture, Mechanical Equipment)
                        // Bounding Box giao nhau nhưng không có trục dài -> không phân loại góc được.
                        // Theo kế hoạch, point-based element không có direction -> return null -> skip, không report clash song song.
                        continue;
                    }

                    // Biến đổi vector hướng của MEP từ hệ tọa độ Link sang Host
                    XYZ mepDir = linkTransform.OfVector(mepDirLink).Normalize();

                    AngleClassifier.AngleClass angleClass =
                        AngleClassifier.Classify(wallDir, mepDir, settings.ParallelThresholdDeg);

                    // Chỉ phát hiện va chạm SONG SONG
                    if (angleClass == AngleClassifier.AngleClass.Parallel)
                    {
                        double angleDeg = AngleClassifier.GetAngleDeg(wallDir, mepDir);
                        results.Add(BuildResult(wall, mep, angleDeg, wallBB, mepBBHost, hostDoc));
                    }
                }
            }

            return results;
        }

        private ClashResult BuildResult(
            Wall wall,
            Element mep,
            double angleDeg,
            BoundingBoxXYZ wallBB,
            BoundingBoxXYZ mepBBHost,
            Document hostDoc)
        {
            string levelName = "Unknown";
            ElementId levelId = wall.LevelId;
            if (levelId != ElementId.InvalidElementId)
            {
                Level lvl = hostDoc.GetElement(levelId) as Level;
                if (lvl != null)
                {
                    levelName = lvl.Name;
                }
            }

            XYZ clashMid = GetClashMidpoint(wallBB, mepBBHost);

            return new ClashResult
            {
                HostWallId = (int)wall.Id.Value,
                LinkMepId = (int)mep.Id.Value,
                WallTypeName = wall.WallType?.Name ?? "Wall",
                MepName = mep.Name,
                LevelName = levelName,
                AngleDeg = Math.Round(angleDeg, 1),
                ClashMidpoint = clashMid
            };
        }

        private static XYZ GetClashMidpoint(BoundingBoxXYZ a, BoundingBoxXYZ b)
        {
            if (a == null || b == null) return new XYZ(0, 0, 0);

            double minX = Math.Max(a.Min.X, b.Min.X);
            double minY = Math.Max(a.Min.Y, b.Min.Y);
            double minZ = Math.Max(a.Min.Z, b.Min.Z);

            double maxX = Math.Min(a.Max.X, b.Max.X);
            double maxY = Math.Min(a.Max.Y, b.Max.Y);
            double maxZ = Math.Min(a.Max.Z, b.Max.Z);

            return new XYZ((minX + maxX) / 2.0, (minY + maxY) / 2.0, (minZ + maxZ) / 2.0);
        }
    }
}
