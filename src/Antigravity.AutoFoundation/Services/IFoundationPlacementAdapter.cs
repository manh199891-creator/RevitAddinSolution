using System;

namespace Antigravity.AutoFoundation.Services
{
    public interface IFoundationPlacementAdapter
    {
        bool TryResolveLevel(double elevation, out double levelElevation);
        bool TryGetOrCreateFoundationType(double length, double width, out string typeName);
        bool PlaceFoundation(double x, double y, double z, string typeName, double rotationAngle);
    }
}
