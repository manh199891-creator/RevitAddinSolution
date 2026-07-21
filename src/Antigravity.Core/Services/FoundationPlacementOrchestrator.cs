using System;
using System.Collections.Generic;
using Antigravity.Core.Models;

namespace Antigravity.Core.Services
{
    public class FoundationPlacementOrchestrator
    {
        private readonly IFoundationPlacementAdapter _adapter;
        private readonly ICadParserService _parserService;

        public FoundationPlacementOrchestrator(IFoundationPlacementAdapter adapter, ICadParserService parserService)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _parserService = parserService ?? throw new ArgumentNullException(nameof(parserService));
        }

        public int ExtractAndPlaceFoundations(object cadLink, string layerName)
        {
            var data = _parserService.ExtractFoundationData(cadLink, layerName);
            return PlaceFoundations(data);
        }

        public int PlaceFoundations(IEnumerable<FoundationData> foundationsData)
        {
            if (foundationsData == null) throw new ArgumentNullException(nameof(foundationsData));

            int count = 0;
            foreach (var data in foundationsData)
            {
                if (data == null) continue;
                
                if (double.IsNaN(data.X) || double.IsInfinity(data.X) ||
                    double.IsNaN(data.Y) || double.IsInfinity(data.Y) ||
                    double.IsNaN(data.Z) || double.IsInfinity(data.Z) ||
                    double.IsNaN(data.Length) || double.IsInfinity(data.Length) ||
                    double.IsNaN(data.Width) || double.IsInfinity(data.Width) ||
                    double.IsNaN(data.RotationAngle) || double.IsInfinity(data.RotationAngle))
                {
                    continue;
                }

                if (data.Length <= 0 || data.Width <= 0)
                {
                    continue;
                }

                double levelElevation;
                if (!_adapter.TryResolveLevel(data.Z, out levelElevation)
                    || double.IsNaN(levelElevation)
                    || double.IsInfinity(levelElevation))
                {
                    continue;
                }

                if (_adapter.TryGetOrCreateFoundationType(data.Length, data.Width, out var typeName))
                {
                    if (_adapter.PlaceFoundation(data.X, data.Y, data.Z, typeName, data.RotationAngle))
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
