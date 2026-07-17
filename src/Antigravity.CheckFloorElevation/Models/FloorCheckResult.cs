namespace Antigravity.CheckFloorElevation.Models
{
    public class FloorCheckResult
    {
        public int HostFloorId { get; set; }

        public int LinkFloorId { get; set; } = -1;

        public string LevelName { get; set; }

        public string HostTypeName { get; set; }

        public string LinkTypeName { get; set; }

        public double ZTopHostMm { get; set; }

        public double ZTopLinkMm { get; set; }

        public double DeltaZMm
        {
            get { return ZTopHostMm - ZTopLinkMm; }
        }

        public bool IsError { get; set; }

        public bool IsNoMatch { get; set; }

        public string ErrorMessage { get; set; }
    }
}
