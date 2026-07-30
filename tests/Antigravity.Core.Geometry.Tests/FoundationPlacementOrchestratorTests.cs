using System;
using System.Collections.Generic;
using Antigravity.Core.Geometry;
using Antigravity.AutoFoundation.Geometry;

using Antigravity.AutoFoundation.Models;
using Antigravity.Core.Services;
using Antigravity.AutoFoundation.Services;
using Xunit;

namespace Antigravity.Core.Geometry.Tests
{
    public class FoundationPlacementOrchestratorTests
    {
        private class FakeFoundationPlacementAdapter : IFoundationPlacementAdapter
        {
            public List<PlacementRequest> Placements = new List<PlacementRequest>();

            public bool TryResolveLevel(double elevation, out double levelElevation)
            {
                levelElevation = elevation;
                return true;
            }

            public bool TryGetOrCreateFoundationType(double length, double width, out string typeName)
            {
                typeName = $"Foundation_{Math.Round(length * 304.8)}_{Math.Round(width * 304.8)}";
                return true;
            }

            public bool PlaceFoundation(double x, double y, double z, string typeName, double rotationAngle)
            {
                Placements.Add(new PlacementRequest
                {
                    X = x,
                    Y = y,
                    Z = z,
                    TypeName = typeName,
                    RotationAngle = rotationAngle
                });
                return true;
            }
        }

        private class PlacementRequest
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }
            public string TypeName { get; set; }
            public double RotationAngle { get; set; }
        }

        [Fact]
        public void PlaceFoundations_ValidatesPlacementRequests()
        {
            var adapter = new FakeFoundationPlacementAdapter();
            var orchestrator = new FoundationPlacementOrchestrator(adapter, new FakeCadParserService());

            var foundations = new List<FoundationData>
            {
                new FoundationData { X = 10, Y = 20, Z = 15.5, Length = 10, Width = 5, RotationAngle = Math.PI / 4 }
            };

            orchestrator.PlaceFoundations(foundations);

            Assert.Single(adapter.Placements);
            var req = adapter.Placements[0];
            
            Assert.Equal(10, req.X);
            Assert.Equal(20, req.Y);
            Assert.Equal(15.5, req.Z);
            Assert.Equal(Math.PI / 4, req.RotationAngle);
            Assert.Equal($"Foundation_{Math.Round(10 * 304.8)}_{Math.Round(5 * 304.8)}", req.TypeName);
        }

        [Fact]
        public void PlaceFoundations_IgnoresInvalidDimensions()
        {
            var adapter = new FakeFoundationPlacementAdapter();
            var orchestrator = new FoundationPlacementOrchestrator(adapter, new FakeCadParserService());

            var foundations = new List<FoundationData>
            {
                new FoundationData { X = 10, Y = 20, Z = 15.5, Length = 0, Width = 5, RotationAngle = 0 }, // Invalid Length
                new FoundationData { X = 10, Y = 20, Z = 15.5, Length = 10, Width = 0, RotationAngle = 0 }, // Invalid Width
                new FoundationData { X = 10, Y = 20, Z = 15.5, Length = -5, Width = 5, RotationAngle = 0 } // Negative Length
            };

            orchestrator.PlaceFoundations(foundations);

            Assert.Empty(adapter.Placements);
        }

        [Fact]
        public void PlaceFoundations_IgnoresInvalidNumbers()
        {
            var adapter = new FakeFoundationPlacementAdapter();
            var orchestrator = new FoundationPlacementOrchestrator(adapter, new FakeCadParserService());

            var foundations = new List<FoundationData>
            {
                new FoundationData { X = double.NaN, Y = 20, Z = 15.5, Length = 10, Width = 5, RotationAngle = 0 }, 
                new FoundationData { X = 10, Y = double.PositiveInfinity, Z = 15.5, Length = 10, Width = 5, RotationAngle = 0 },
                new FoundationData { X = 10, Y = 20, Z = double.NaN, Length = 10, Width = 5, RotationAngle = 0 },
                new FoundationData { X = 10, Y = 20, Z = 15.5, Length = double.PositiveInfinity, Width = 5, RotationAngle = 0 },
                new FoundationData { X = 10, Y = 20, Z = 15.5, Length = 10, Width = double.NaN, RotationAngle = 0 },
                new FoundationData { X = 10, Y = 20, Z = 15.5, Length = 10, Width = 5, RotationAngle = double.NegativeInfinity }
            };

            orchestrator.PlaceFoundations(foundations);

            Assert.Empty(adapter.Placements);
        }

        private class FailingAdapter : IFoundationPlacementAdapter
        {
            public bool WasPlaceFoundationCalled { get; private set; }

            public bool TryResolveLevel(double elevation, out double levelElevation)
            {
                levelElevation = elevation;
                return true;
            }

            public bool TryGetOrCreateFoundationType(double length, double width, out string typeName)
            {
                typeName = null;
                return false;
            }

            public bool PlaceFoundation(double x, double y, double z, string typeName, double rotationAngle)
            {
                WasPlaceFoundationCalled = true;
                return true;
            }
        }

        [Fact]
        public void PlaceFoundations_SkipsPlacementIfAdapterFailsToCreateType()
        {
            var adapter = new FailingAdapter();
            var orchestrator = new FoundationPlacementOrchestrator(adapter, new FakeCadParserService());

            var foundations = new List<FoundationData>
            {
                new FoundationData { X = 10, Y = 20, Z = 15.5, Length = 10, Width = 5, RotationAngle = 0 }
            };

            var exception = Record.Exception(() => orchestrator.PlaceFoundations(foundations));
            Assert.Null(exception); // Should gracefully continue
            Assert.False(adapter.WasPlaceFoundationCalled);
        }

        private class PlaceFailingAdapter : IFoundationPlacementAdapter
        {
            public int TryGetOrCreateCalls { get; private set; }
            public int PlaceFoundationCalls { get; private set; }

            public bool TryResolveLevel(double elevation, out double levelElevation)
            {
                levelElevation = elevation;
                return true;
            }

            public bool TryGetOrCreateFoundationType(double length, double width, out string typeName)
            {
                TryGetOrCreateCalls++;
                typeName = "ValidType";
                return true;
            }

            public bool PlaceFoundation(double x, double y, double z, string typeName, double rotationAngle)
            {
                PlaceFoundationCalls++;
                return false; // Simulate failure during SubTransaction (e.g., Rotation fails)
            }
        }

        [Fact]
        public void PlaceFoundations_SkipsPlacementIfAdapterFailsToPlace()
        {
            var adapter = new PlaceFailingAdapter();
            var orchestrator = new FoundationPlacementOrchestrator(adapter, new FakeCadParserService());

            var foundations = new List<FoundationData>
            {
                new FoundationData { X = 10, Y = 20, Z = 15.5, Length = 10, Width = 5, RotationAngle = 0 },
                new FoundationData { X = 20, Y = 30, Z = 15.5, Length = 10, Width = 5, RotationAngle = 0 }
            };

            var count = orchestrator.PlaceFoundations(foundations);
            
            Assert.Equal(0, count); // None succeeded
            Assert.Equal(2, adapter.TryGetOrCreateCalls);
            Assert.Equal(2, adapter.PlaceFoundationCalls); // It should continue to the next one even if PlaceFoundation returns false
        }

        [Fact]
        public void EndToEnd_CadParser_ExtractsAndOrchestratorPlaces()
        {
            var segments = new List<Segment2>
            {
                // Axis-aligned
                new Segment2(new Point2(0, 0), new Point2(10, 0)),
                new Segment2(new Point2(10, 0), new Point2(10, 5)),
                new Segment2(new Point2(10, 5), new Point2(0, 5)),
                new Segment2(new Point2(0, 5), new Point2(0, 0))
            };

            var rotatedSegments = new List<Segment2>
            {
                // Rotated by Atan2(6, 8)
                new Segment2(new Point2(7.5, 5), new Point2(15.5, 11)),
                new Segment2(new Point2(15.5, 11), new Point2(12.5, 15)),
                new Segment2(new Point2(12.5, 15), new Point2(4.5, 9)),
                new Segment2(new Point2(4.5, 9), new Point2(7.5, 5))
            };

            var allSegments = new List<Segment2>();
            allSegments.AddRange(segments);
            allSegments.AddRange(rotatedSegments);

            // 1. Parser extracts data
            var extractedData = CadParserService.ExtractFromSegments(allSegments, elevation: 12.5);

            // 2. Orchestrator processes it
            var adapter = new FakeFoundationPlacementAdapter();
            var orchestrator = new FoundationPlacementOrchestrator(adapter, new FakeCadParserService());
            
            orchestrator.PlaceFoundations(extractedData);

            // 3. Verify
            Assert.Equal(2, adapter.Placements.Count);
            
            var req1 = adapter.Placements[0];
            Assert.Equal(5, req1.X, 3);
            Assert.Equal(2.5, req1.Y, 3);
            Assert.Equal(12.5, req1.Z);
            Assert.Equal(0, req1.RotationAngle, 3);
            Assert.Equal($"Foundation_{Math.Round(10 * 304.8)}_{Math.Round(5 * 304.8)}", req1.TypeName);

            var req2 = adapter.Placements[1];
            Assert.Equal(10, req2.X, 3);
            Assert.Equal(10, req2.Y, 3);
            Assert.Equal(12.5, req2.Z);
            Assert.Equal(Math.Atan2(6, 8), req2.RotationAngle, 3);
            Assert.Equal($"Foundation_{Math.Round(10 * 304.8)}_{Math.Round(5 * 304.8)}", req2.TypeName);
        }
        [Fact]
        public void PlaceFoundations_ThrowsArgumentNullException_OnNullData()
        {
            var adapter = new FakeFoundationPlacementAdapter();
            var orchestrator = new FoundationPlacementOrchestrator(adapter, new FakeCadParserService());

            Assert.Throws<ArgumentNullException>(() => orchestrator.PlaceFoundations(null));
        }

        [Fact]
        public void PlaceFoundations_SkipsTypeCreationWhenElevationCannotResolve()
        {
            var adapter = new UnresolvedLevelAdapter();
            var orchestrator = new FoundationPlacementOrchestrator(adapter, new FakeCadParserService());

            var count = orchestrator.PlaceFoundations(new[]
            {
                new FoundationData { X = 1, Y = 2, Z = 3, Length = 10, Width = 5 }
            });

            Assert.Equal(0, count);
            Assert.False(adapter.TypeCreationCalled);
        }

        private class UnresolvedLevelAdapter : IFoundationPlacementAdapter
        {
            public bool TypeCreationCalled { get; private set; }

            public bool TryResolveLevel(double elevation, out double levelElevation)
            {
                levelElevation = double.NaN;
                return false;
            }

            public bool TryGetOrCreateFoundationType(double length, double width, out string typeName)
            {
                TypeCreationCalled = true;
                typeName = null;
                return false;
            }

            public bool PlaceFoundation(double x, double y, double z, string typeName, double rotationAngle)
            {
                return false;
            }
        }

        public class FakeCadParserService : ICadParserService
        {
            public bool WasExtractCalled { get; private set; }
            public List<FoundationData> ExtractedData { get; } = new List<FoundationData>();

            public List<FoundationData> ExtractFoundationData(object cadLink, string layerName)
            {
                WasExtractCalled = true;
                return ExtractedData;
            }
        }

        [Fact]
        public void ExtractAndPlaceFoundations_PassesDataToPlaceFoundations()
        {
            var adapter = new FakeFoundationPlacementAdapter();
            var parser = new FakeCadParserService();
            var orchestrator = new FoundationPlacementOrchestrator(adapter, parser);
            
            parser.ExtractedData.Add(new FoundationData { X = 1, Y = 2, Z = 3, Length = 10, Width = 5 });

            // We can pass null for ImportInstance because FakeCadParserService ignores it
            int count = orchestrator.ExtractAndPlaceFoundations(null, "S-FND");

            Assert.True(parser.WasExtractCalled);
            Assert.Equal(1, count);
            Assert.Single(adapter.Placements);
            Assert.Equal(1, adapter.Placements[0].X);
        }
    }
}
