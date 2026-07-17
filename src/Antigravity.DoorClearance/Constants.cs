using Autodesk.Revit.DB;

namespace DoorClearanceBox
{
    /// <summary>
    /// Single source of truth for all constants, GUIDs, and configuration values.
    /// All geometry defaults follow the W × 2W × H formula documented in the design spec.
    /// </summary>
    public static class Constants
    {
        // ── Add-in Identity ─────────────────────────────────────────────────────
        public const string AddInName  = "Clearance Box (Doors/Windows)";
        public const string VendorId   = "AGRAV";
        public const string VendorDesc = "Antigravity BIM Tools";
        public const string TabName    = "BIM Tools";
        public const string PanelName  = "Clearance";
        public const string Version    = "1.6.1";
        
        public enum GeometryType
        {
            Rectangle, // Hbox=H, Lbox=2L
            Ellipse    // a=H, b=2L
        }

        public static GeometryType DefaultGeometry = GeometryType.Rectangle;

        // ── Unique GUIDs ────────────────────────────────────────────────────────
        // These are now unique to avoid clashes with other placeholder add-ins.
        public const string AppGuid     = "8F2A1E86-5B4D-4F71-B53E-C9A1A2B3C4D5";
        public const string UpdaterGuid = "D6F3E4B2-9A8C-4D7F-A1B2-C3D4E5F6A7B8";

        // ── Ribbon ──────────────────────────────────────────────────────────────
        public const string CmdCreateName = "Create\nClearance";
        public const string CmdClearName  = "Clear\nClearance";
        public const string CmdCreateTip  = "Generates W × 2W × H DirectShape clearance volumes at each door.";
        public const string CmdClearTip   = "Removes all clearance boxes created by this tool.";

        // ── Graphics ────────────────────────────────────────────────────────────
        public const string SubCategoryName = "Door Clearance Volume";
        public const byte   Transparency    = 50; // 0-100 (50 = semi-transparent)
        public static readonly Color ColorBlue = new Color(0, 120, 215);

        // ── DirectShape Tracking ─────────────────────────────────────────────────
        /// <summary>
        /// Prefix written to the Mark parameter of each DirectShape.
        /// Format: "DCB:&lt;doorId&gt;"  e.g. "DCB:123456"
        /// </summary>
        public const string MarkPrefix = "DCB:";

        /// <summary>
        /// Written to the Comments parameter when the updater detects that
        /// the source door's dimensions have changed since the shape was created.
        /// </summary>
        public const string StaleFlag = "⚠ STALE – source door modified. Please recreate.";

        // ── Geometry ────────────────────────────────────────────────────────────
        /// <summary>
        /// Clearance depth multiplier applied to door width on EACH SIDE of the wall face.
        ///   • Total box depth  = 2 × ClearanceMultiplier × W
        ///   • Default 1.0 → W inside + W outside = 2W total (symmetric, no swing needed)
        /// </summary>
        public const double ClearanceMultiplier = 1.0;

        /// <summary>Fallback door width (mm) when the parameter is missing or zero.</summary>
        public const double DefaultWidthMm  = 900.0;

        /// <summary>Fallback door height (mm) when the parameter is missing or zero.</summary>
        public const double DefaultHeightMm = 2100.0;

        // ── IFC ─────────────────────────────────────────────────────────────────
        /// <summary>Name of the IFC shared parameter to look for on DirectShapes.</summary>
        public const string IfcExportAsParam = "IfcExportAs";

        /// <summary>Value written to IfcExportAs so downstream IFC exporters map to IfcSpace.</summary>
        public const string IfcExportAsValue = "IfcSpace";
    }
}
