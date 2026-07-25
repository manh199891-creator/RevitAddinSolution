using System;
using System.Collections.Generic;
using System.Linq;
using Antigravity.Formwork.Core.Models;

namespace Antigravity.Formwork.Core.Solvers
{
    public class PanelizationSolver
    {
        private readonly FormworkPanelSystem _system;

        public PanelizationSolver(FormworkPanelSystem system)
        {
            _system = system;
        }

        public PlacementResult Solve(ConcreteHostDto host)
        {
            var result = new PlacementResult
            {
                HostUniqueId = host.HostUniqueId,
                HostCategory = host.HostCategory,
                FaceKey = host.FaceKey,
                CycleId = host.CycleId,
                SystemId = _system.SystemId,
                PlacedPanels = new List<PanelDto>()
            };

            double dx = host.EndX - host.StartX;
            double dy = host.EndY - host.StartY;
            double totalLength = Math.Sqrt(dx * dx + dy * dy);
            double angle = Math.Atan2(dy, dx);

            // Convert total length to millimeters for calculation (assuming host coords are in millimeters)
            double remainingLength = totalLength;
            double currentPos = 0;

            var sortedWidths = _system.AvailableWidths.OrderByDescending(w => w).ToList();

            // Simple greedy solver for the MVP
            while (remainingLength > 0.1)
            {
                double? selectedWidth = null;
                bool isFiller = false;

                // Try to find the largest standard panel that fits
                foreach (var width in sortedWidths)
                {
                    if (remainingLength >= width - 0.1) // 0.1mm tolerance
                    {
                        selectedWidth = width;
                        break;
                    }
                }

                // If no standard panel fits, use a filler for the remainder
                if (selectedWidth == null)
                {
                    selectedWidth = remainingLength;
                    isFiller = true;
                }

                // Calculate center point of this panel
                double centerDist = currentPos + (selectedWidth.Value / 2.0);
                double centerX = host.StartX + centerDist * Math.Cos(angle);
                double centerY = host.StartY + centerDist * Math.Sin(angle);
                double centerZ = host.BaseElevation + (_system.StandardHeight / 2.0);

                result.PlacedPanels.Add(new PanelDto
                {
                    CatalogItemId = isFiller ? $"FILLER_{selectedWidth.Value:F0}" : $"PANEL_{selectedWidth.Value:F0}",
                    CenterX = centerX,
                    CenterY = centerY,
                    CenterZ = centerZ,
                    RotationAngle = angle,
                    Width = selectedWidth.Value,
                    Height = _system.StandardHeight,
                    IsFiller = isFiller
                });

                currentPos += selectedWidth.Value;
                remainingLength -= selectedWidth.Value;
            }

            return result;
        }
    }
}
