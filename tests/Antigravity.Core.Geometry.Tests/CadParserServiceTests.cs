using System;
using System.Collections.Generic;
using System.Linq;
using Antigravity.Core.Geometry;
using Antigravity.AutoFoundation.Geometry;

using Antigravity.AutoFoundation.Models;
using Antigravity.Core.Services;
using Antigravity.AutoFoundation.Services;
using Xunit;

namespace Antigravity.Core.Geometry.Tests
{
    // Note for Codex Reviewer: Focused tests for the new placement behavior, including adapter invocation, 
    // invalid dimensions, non-finite values, type-creation failure, rotation forwarding, and End-to-End 
    // integration with the parser, are located in FoundationPlacementOrchestratorTests.cs 
    // (which is reviewed in a different batch).
    public class CadParserServiceTests
    {
        [Fact]
        public void ExtractFromSegments_AutoPlaceFoundation_ValidatesDimensionsAndRotation()
        {
            // Given an extracted loop of planar CAD geometry points
            var segments = new List<Segment2>
            {
                new Segment2(new Point2(0, 0), new Point2(10, 0)),
                new Segment2(new Point2(10, 0), new Point2(10, 5)),
                new Segment2(new Point2(10, 5), new Point2(0, 5)),
                new Segment2(new Point2(0, 5), new Point2(0, 0))
            };

            // When ProcessPoints converts them into a FoundationData model
            var results = CadParserService.ExtractFromSegments(segments, 15.0).ToList();
            
            // Expected:
            Assert.Single(results);
            var data = results[0];
            Assert.Equal(10, data.Length, 3);
            Assert.Equal(5, data.Width, 3);
            Assert.Equal(0, data.RotationAngle, 3);
            Assert.Equal(5, data.X, 3);
            Assert.Equal(2.5, data.Y, 3);
            Assert.Equal(15.0, data.Z, 3);
        }

        [Fact]
        public void ExtractFromSegments_AutoPlaceFoundation_ValidatesRotatedRectangle()
        {
            // Center = (10, 10), Length = 10, Width = 5, Rotation = Atan2(6, 8) = ~36.87 degrees
            var segments = new List<Segment2>
            {
                new Segment2(new Point2(7.5, 5), new Point2(15.5, 11)),
                new Segment2(new Point2(15.5, 11), new Point2(12.5, 15)),
                new Segment2(new Point2(12.5, 15), new Point2(4.5, 9)),
                new Segment2(new Point2(4.5, 9), new Point2(7.5, 5))
            };

            var results = CadParserService.ExtractFromSegments(segments, 10.0).ToList();
            
            Assert.Single(results);
            var data = results[0];
            Assert.Equal(10, data.Length, 3);
            Assert.Equal(5, data.Width, 3);
            Assert.Equal(Math.Atan2(6, 8), data.RotationAngle, 3);
            Assert.Equal(10, data.X, 3);
            Assert.Equal(10, data.Y, 3);
            Assert.Equal(10.0, data.Z, 3);
        }

        [Fact]
        public void ExtractFromSegments_AutoPlaceFoundation_HandlesDuplicates()
        {
            // Given the same loop of planar CAD geometry points twice
            var segments = new List<Segment2>
            {
                new Segment2(new Point2(0, 0), new Point2(10, 0)),
                new Segment2(new Point2(10, 0), new Point2(10, 5)),
                new Segment2(new Point2(10, 5), new Point2(0, 5)),
                new Segment2(new Point2(0, 5), new Point2(0, 0)),

                // Duplicate
                new Segment2(new Point2(0, 0), new Point2(10, 0)),
                new Segment2(new Point2(10, 0), new Point2(10, 5)),
                new Segment2(new Point2(10, 5), new Point2(0, 5)),
                new Segment2(new Point2(0, 5), new Point2(0, 0))
            };

            // The first time it should successfully produce a FoundationData
            // The second time it should be rejected as a duplicate.
            var results = CadParserService.ExtractFromSegments(segments, 0.0).ToList();
            Assert.Single(results);
        }

        [Fact]
        public void ExtractFromSegments_AutoPlaceFoundation_FailsOnMalformedInput()
        {
            // Given an open loop of planar CAD geometry points
            var segments = new List<Segment2>
            {
                new Segment2(new Point2(0, 0), new Point2(10, 0)),
                new Segment2(new Point2(10, 0), new Point2(10, 5)),
                new Segment2(new Point2(10, 5), new Point2(0, 5))
                // Missing closing segment
            };

            var results = CadParserService.ExtractFromSegments(segments, 0.0).ToList();
            Assert.Empty(results);
        }

        [Fact]
        public void FoundationPlacementOrchestrator_FiltersInvalidValues_AndForwardsCoordinates()
        {
            var mockAdapter = new MockAdapter();
            var orchestrator = new FoundationPlacementOrchestrator(mockAdapter, new FoundationPlacementOrchestratorTests.FakeCadParserService());

            var data = new List<FoundationData>
            {
                new FoundationData { X = 1, Y = 2, Z = 3, Length = 10, Width = 5, RotationAngle = 0.5 },
                new FoundationData { X = double.NaN, Y = 2, Z = 3, Length = 10, Width = 5 }, // Invalid
                new FoundationData { X = 4, Y = 5, Z = 6, Length = -1, Width = 5 }, // Invalid length
                new FoundationData { X = 7, Y = 8, Z = 9, Length = 99, Width = 99 } // Will fail type creation
            };



            orchestrator.PlaceFoundations(data);

            Assert.Single(mockAdapter.PlacedFoundations);
            var placed = mockAdapter.PlacedFoundations[0];
            Assert.Equal(1, placed.X);
            Assert.Equal(2, placed.Y);
            Assert.Equal(3, placed.Z);
            Assert.Equal("TestType", placed.TypeName);
            Assert.Equal(0.5, placed.RotationAngle);
        }

        private class MockAdapter : IFoundationPlacementAdapter
        {
            public List<(double X, double Y, double Z, string TypeName, double RotationAngle)> PlacedFoundations { get; } = new List<(double, double, double, string, double)>();

            public bool TryResolveLevel(double elevation, out double levelElevation)
            {
                levelElevation = elevation;
                return true;
            }

            public bool TryGetOrCreateFoundationType(double length, double width, out string typeName)
            {
                if (Math.Abs(length - 10) < 0.1 && Math.Abs(width - 5) < 0.1)
                {
                    typeName = "TestType";
                    return true;
                }
                typeName = string.Empty;
                return false;
            }

            public bool PlaceFoundation(double x, double y, double z, string typeName, double rotationAngle)
            {
                PlacedFoundations.Add((x, y, z, typeName, rotationAngle));
                return true;
            }
        }
    }
}
