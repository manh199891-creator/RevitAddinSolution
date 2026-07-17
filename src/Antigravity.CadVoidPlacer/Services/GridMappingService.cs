using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Antigravity.CadVoidPlacer.Services
{
    /// <summary>
    /// Handles coordinate transformation from CAD space (mm) to Revit model space (feet).
    /// </summary>
    public class GridMappingService
    {
        private const double MM_TO_FEET = 1.0 / 304.8;

        private readonly double _cadOriginX;
        private readonly double _cadOriginY;
        private readonly XYZ    _revitOrigin;

        public GridMappingService(double cadOriginX, double cadOriginY, XYZ revitOrigin)
        {
            _cadOriginX  = cadOriginX;
            _cadOriginY  = cadOriginY;
            _revitOrigin = revitOrigin ?? XYZ.Zero;
        }

        /// <summary>Transforms one CAD point (mm) to a Revit XYZ (feet).</summary>
        public XYZ CadToRevit(double cadX, double cadY, double elevationFeet = 0.0)
        {
            double dx = (cadX - _cadOriginX) * MM_TO_FEET;
            double dy = (cadY - _cadOriginY) * MM_TO_FEET;
            return new XYZ(_revitOrigin.X + dx, _revitOrigin.Y + dy, _revitOrigin.Z + elevationFeet);
        }

        /// <summary>
        /// Transforms a list of CAD polygon vertices (mm) into an array of Revit XYZ points (feet).
        /// </summary>
        public XYZ[] CadVerticesToRevit(List<double[]> cadVertices, double elevationFeet = 0.0)
        {
            var result = new XYZ[cadVertices.Count];
            for (int i = 0; i < cadVertices.Count; i++)
                result[i] = CadToRevit(cadVertices[i][0], cadVertices[i][1], elevationFeet);
            return result;
        }

        public static double MmToFeet(double mm)   => mm * MM_TO_FEET;
        public static double FeetToMm(double feet)  => feet / MM_TO_FEET;
    }
}
