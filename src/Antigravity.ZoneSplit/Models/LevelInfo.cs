namespace Antigravity.ZoneSplit.Models
{
    /// <summary>
    /// DTO thông tin một Level trong Revit model.
    /// Dùng int thay ElementId — convert tại Helpers layer.
    /// </summary>
    public sealed class LevelInfo
    {
        /// <summary>ElementId.Value của Level.</summary>
        public int    Id        { get; }
        public string Name      { get; }
        /// <summary>Cao độ Level — Revit internal units (feet).</summary>
        public double Elevation { get; }

        public LevelInfo(int id, string name, double elevation)
        {
            Id        = id;
            Name      = name;
            Elevation = elevation;
        }

        public override string ToString()
            => $"Level[{Id}] '{Name}' @ {Elevation:F2} ft ({Elevation * 304.8:F0} mm)";
    }
}
