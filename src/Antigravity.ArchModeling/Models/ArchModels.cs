using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Antigravity.ArchModeling.Models
{
    public class BlockInfo
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double RotationRad { get; set; }
        public string BlockName { get; set; }
        public string LayerName { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double ScaleX { get; set; }
        public double ScaleY { get; set; }
        public bool IsFlippedX { get; set; }
        public bool IsFlippedY { get; set; }
        /// <summary>CAD entity handle (hex string) used for red-marking ambiguous blocks.</summary>
        public string EntityHandle { get; set; }
        
        // Internal geometry vector pointing to swing direction (World space)
        public double FacingVectorX { get; set; }
        public double FacingVectorY { get; set; }

        public BlockInfo(double x, double y, double rotationRad, string blockName, string layerName,
            double centerX = 0, double centerY = 0,
            double scaleX = 1.0, double scaleY = 1.0,
            bool isFlippedX = false, bool isFlippedY = false,
            string entityHandle = null,
            double facingVecX = 0, double facingVecY = 0)
        {
            X = x;
            Y = y;
            RotationRad = rotationRad;
            BlockName = blockName;
            LayerName = layerName;
            CenterX = centerX == 0 ? x : centerX;
            CenterY = centerY == 0 ? y : centerY;
            ScaleX = scaleX;
            ScaleY = scaleY;
            IsFlippedX = isFlippedX;
            IsFlippedY = isFlippedY;
            EntityHandle = entityHandle;
            FacingVectorX = facingVecX;
            FacingVectorY = facingVecY;
        }
    }

    public class WallData
    {
        public List<Curve> Boundaries { get; set; } = new List<Curve>();
        public double ThicknessMm { get; set; }
        public Curve CenterLine { get; set; }

        public WallData(List<Curve> boundaries, double thicknessMm, Curve centerLine)
        {
            Boundaries = boundaries;
            ThicknessMm = thicknessMm;
            CenterLine = centerLine;
        }
    }
}
