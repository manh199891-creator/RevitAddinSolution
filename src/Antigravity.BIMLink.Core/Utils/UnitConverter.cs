namespace Antigravity.BIMLink.Core.Utils
{
    public static class UnitConverter
    {
        private const double FeetToMeters = 0.3048;

        public static double RevitLengthToMetric(double feetValue)
        {
            return feetValue * FeetToMeters;
        }

        public static double MetricLengthToRevit(double meterValue)
        {
            return meterValue / FeetToMeters;
        }
    }
}
