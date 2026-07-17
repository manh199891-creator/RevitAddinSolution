using System;
using System.Collections.Generic;

namespace Antigravity.CadSleevePlacer.Models
{
    /// <summary>
    /// Represents a single sleeve location extracted from CAD annotations.
    /// </summary>
    public class CadSleeveInfo
    {
        public string LayerName { get; set; }

        /// <summary>
        /// The insertion point (X, Y, Z in mm) from AutoCAD. 
        /// Usually corresponds to the target point of a Leader or the insertion point of a Block.
        /// </summary>
        public double[] TargetPoint { get; set; }

        /// <summary>
        /// Extracted diameter from the annotation (e.g. DN125 -> 125).
        /// </summary>
        public double DiameterMm { get; set; }

        /// <summary>
        /// Raw elevation value extracted from annotation (relative to floor level, in mm).
        /// Interpretation depends on ElevationType:
        ///   "COP" or "TOP" → already the center elevation.
        ///   "BOO", "BOD", "BOP" → bottom-of-opening/duct/pipe elevation; center = ElevationMm + size/2.
        /// </summary>
        public double ElevationMm { get; set; }

        /// <summary>
        /// Type of elevation annotation found in CAD text.
        /// Values: "COP" (center of pipe), "BOO" (bottom of opening), "BOD" (bottom of duct),
        ///         "BOP" (bottom of pipe), "TOP" (top of opening), "" (unknown → treated as center).
        /// </summary>
        public string ElevationType { get; set; } = "";

        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public bool IsRectangular { get; set; }
        public double RotationRad { get; set; }

        /// <summary>
        /// Any additional extracted label or sleeve type (e.g. SLEEVE-P-01).
        /// </summary>
        public string SleeveType { get; set; }

        // Constructor cho Sleeve tròn
        public CadSleeveInfo(double[] targetPoint, double diameter, double elevation, string elevationType = "", string type = "", string layer = "", double rotationRad = 0.0)
        {
            TargetPoint = targetPoint;
            DiameterMm = diameter;
            ElevationMm = elevation;
            ElevationType = elevationType ?? "";
            IsRectangular = false;
            SleeveType = type;
            LayerName = layer;
            RotationRad = rotationRad;
        }

        // Constructor cho Sleeve chữ nhật
        public CadSleeveInfo(double[] targetPoint, double width, double height, double elevation, string elevationType = "", string type = "", string layer = "", double rotationRad = 0.0)
        {
            TargetPoint = targetPoint;
            WidthMm = width;
            HeightMm = height;
            ElevationMm = elevation;
            ElevationType = elevationType ?? "";
            IsRectangular = true;
            SleeveType = type;
            LayerName = layer;
            RotationRad = rotationRad;
        }

        public override string ToString()
        {
            string elevLabel = string.IsNullOrEmpty(ElevationType) ? "FL" : ElevationType + "=FL";
            if (IsRectangular)
                return $"Sleeve {SleeveType} - {WidthMm}x{HeightMm} @ {elevLabel}{ElevationMm:+0;-0;+0} - Pt({TargetPoint[0]:F0},{TargetPoint[1]:F0}) - Rot({RotationRad * 180.0 / Math.PI:F1}°)";
            else
                return $"Sleeve {SleeveType} - DN{DiameterMm} @ {elevLabel}{ElevationMm:+0;-0;+0} - Pt({TargetPoint[0]:F0},{TargetPoint[1]:F0}) - Rot({RotationRad * 180.0 / Math.PI:F1}°)";
        }
    }
}
