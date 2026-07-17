using System.Collections.Generic;

namespace Antigravity.CadVoidPlacer.Models
{
    /// <summary>
    /// Represents a single void opening extracted from CAD geometry.
    /// Can be polygon-based (arbitrary shape) or rectangle-based (legacy).
    /// </summary>
    public class CadOpening
    {
        public string LayerName { get; set; }

        /// <summary>
        /// Polygon vertices in CAD coordinate space (mm).
        /// When populated, the void is a prism extruded from this polygon.
        /// </summary>
        public List<double[]> Vertices { get; set; }

        // ── Legacy rectangle fields (kept for compat) ──
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Width   { get; set; }
        public double Length  { get; set; }
        public double Rotation { get; set; }

        /// <summary>True when this opening has polygon vertex data.</summary>
        public bool IsPolygon => Vertices != null && Vertices.Count >= 3;

        /// <summary>Creates a polygon-based opening from a vertex list (CAD mm).</summary>
        public CadOpening(List<double[]> vertices, string layer = "")
        {
            Vertices  = vertices;
            LayerName = layer;

            // Compute centroid for display / naming
            double sumX = 0, sumY = 0;
            foreach (var v in vertices) { sumX += v[0]; sumY += v[1]; }
            CenterX = sumX / vertices.Count;
            CenterY = sumY / vertices.Count;
        }

        /// <summary>Creates a legacy rectangular opening.</summary>
        public CadOpening(double x, double y, double w, double l, double rot = 0)
        {
            CenterX  = x;
            CenterY  = y;
            Width    = w;
            Length   = l;
            Rotation = rot;
        }
    }
}
