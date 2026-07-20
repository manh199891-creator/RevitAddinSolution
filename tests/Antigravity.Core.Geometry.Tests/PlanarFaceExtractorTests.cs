using System;
using System.Collections.Generic;
using System.Linq;
using Antigravity.Core.Geometry;
using Xunit;

namespace Antigravity.Core.Geometry.Tests
{
    public class PlanarFaceExtractorTests
    {
        private readonly PlanarFaceExtractor _extractor = new PlanarFaceExtractor();

        [Fact]
        public void SingleRectangle_ReturnsOneBoundedFace()
        {
            var result = _extractor.Extract(Rectangle(0, 0, 4, 2));

            var face = Assert.Single(result.Faces);
            Assert.Equal(8, face.Area, 6);
            AssertRectangle(face, 4, 2, 2, 1);
        }

        [Fact]
        public void AdjacentRectangles_SharedEdge_ReturnsTwoFacesNotOuterComposite()
        {
            var segments = Rectangle(0, 0, 2, 2)
                .Concat(Rectangle(2, 0, 4, 2))
                .ToList();

            var result = _extractor.Extract(segments);

            Assert.Equal(2, result.Faces.Count);
            Assert.All(result.Faces, face => Assert.Equal(4, face.Area, 6));
            Assert.DoesNotContain(result.Faces, face => Math.Abs(face.Area - 8) < 1e-6);
        }

        [Fact]
        public void OuterRectangleWithDivider_ReturnsTwoCellsOnly()
        {
            var segments = Rectangle(0, 0, 4, 2).ToList();
            segments.Add(Line(2, 0, 2, 2));

            var result = _extractor.Extract(segments);

            Assert.Equal(2, result.Faces.Count);
            Assert.All(result.Faces, face => Assert.Equal(4, face.Area, 6));
        }

        [Fact]
        public void PartialSharedEdge_IsNodedAndReturnsBothRectangles()
        {
            var segments = Rectangle(0, 0, 2, 2)
                .Concat(Rectangle(2, 0.5, 4, 1.5))
                .ToList();

            var result = _extractor.Extract(segments);

            Assert.Equal(2, result.Faces.Count);
            Assert.Equal(new[] { 2.0, 4.0 }, result.Faces.Select(face => face.Area).OrderBy(area => area).ToArray());
        }

        [Fact]
        public void TwoByTwoGrid_ReturnsFourBoundedCells()
        {
            var segments = new List<Segment2>
            {
                Line(0, 0, 2, 0), Line(0, 1, 2, 1), Line(0, 2, 2, 2),
                Line(0, 0, 0, 2), Line(1, 0, 1, 2), Line(2, 0, 2, 2)
            };

            var result = _extractor.Extract(segments);

            Assert.Equal(4, result.Faces.Count);
            Assert.All(result.Faces, face => Assert.Equal(1, face.Area, 6));
        }

        [Fact]
        public void CrossingLines_AreNodedIntoFourCells()
        {
            var segments = Rectangle(0, 0, 4, 4).ToList();
            segments.Add(Line(2, 0, 2, 4));
            segments.Add(Line(0, 2, 4, 2));

            var result = _extractor.Extract(segments);

            Assert.Equal(4, result.Faces.Count);
            Assert.All(result.Faces, face => Assert.Equal(4, face.Area, 6));
        }

        [Fact]
        public void DanglingEdge_DoesNotRemoveValidFace()
        {
            var segments = Rectangle(0, 0, 2, 2).ToList();
            segments.Add(Line(2, 1, 3, 1));

            var result = _extractor.Extract(segments);

            Assert.Single(result.Faces);
            Assert.Equal(4, result.Faces[0].Area, 6);
        }

        [Fact]
        public void DuplicateAndReversedSegments_AreDeduplicated()
        {
            var segments = Rectangle(0, 0, 3, 2).ToList();
            segments.Add(Line(0, 0, 3, 0));
            segments.Add(Line(3, 0, 0, 0));

            var result = _extractor.Extract(segments);

            Assert.Single(result.Faces);
            Assert.Equal(4, result.NormalizedEdgeCount);
        }

        [Fact]
        public void EndpointGapInsideSnapCell_ClosesTheFace()
        {
            var segments = new[]
            {
                Line(0, 0, 2, 0),
                Line(2.0004, 0, 2, 2),
                Line(2, 2, 0, 2),
                Line(0, 2, 0, 0.0004)
            };

            var result = _extractor.Extract(segments);

            Assert.Single(result.Faces);
        }

        [Fact]
        public void InputOrderAndDirection_DoNotChangeOutput()
        {
            var original = Rectangle(0, 0, 2, 2)
                .Concat(Rectangle(2, 0, 5, 2))
                .ToList();
            var random = new Random(12345);
            var shuffled = original
                .OrderBy(_ => random.Next())
                .Select((segment, index) => index % 2 == 0
                    ? new Segment2(segment.End, segment.Start)
                    : segment)
                .ToList();

            var first = _extractor.Extract(original).Faces;
            var second = _extractor.Extract(shuffled).Faces;

            Assert.Equal(first.Count, second.Count);
            Assert.Equal(first.Select(FaceSignature), second.Select(FaceSignature));
        }

        [Fact]
        public void RotatedRectangle_ReturnsStableDimensionsAndRotation()
        {
            var center = new Point2(5, 7);
            var angle = Math.PI / 6;
            var vertices = new[]
            {
                RotateTranslate(-2, -1, center, angle),
                RotateTranslate(2, -1, center, angle),
                RotateTranslate(2, 1, center, angle),
                RotateTranslate(-2, 1, center, angle)
            };
            var segments = Enumerable.Range(0, 4)
                .Select(i => new Segment2(vertices[i], vertices[(i + 1) % 4]));

            var face = Assert.Single(_extractor.Extract(segments).Faces);
            Rectangle2 rectangle;
            Assert.True(Rectangle2.TryCreate(face, out rectangle, distanceTolerance: 0.005));
            Assert.Equal(4, rectangle.Length, 2);
            Assert.Equal(2, rectangle.Width, 2);
            Assert.Equal(center.X, rectangle.Center.X, 2);
            Assert.Equal(center.Y, rectangle.Center.Y, 2);
            Assert.Equal(angle, rectangle.RotationRadians, 2);
        }

        [Fact]
        public void NonRectangularFace_IsRejectedByRectangleSemantics()
        {
            var triangle = new[]
            {
                Line(0, 0, 2, 0), Line(2, 0, 1, 1), Line(1, 1, 0, 0)
            };
            var face = Assert.Single(_extractor.Extract(triangle).Faces);

            Rectangle2 rectangle;
            Assert.False(Rectangle2.TryCreate(face, out rectangle));
        }

        [Fact]
        public void TenByTenGrid_ReturnsOneHundredFaces()
        {
            var segments = new List<Segment2>();
            for (var y = 0; y <= 10; y++) segments.Add(Line(0, y, 10, y));
            for (var x = 0; x <= 10; x++) segments.Add(Line(x, 0, x, 10));

            var result = _extractor.Extract(segments);

            Assert.Equal(100, result.Faces.Count);
            Assert.Equal(220, result.NormalizedEdgeCount);
        }

        [Fact]
        public void ExcessiveInput_FailsFast()
        {
            var extractor = new PlanarFaceExtractor(maxInputSegments: 2);
            var input = new[] { Line(0, 0, 1, 0), Line(1, 0, 1, 1), Line(1, 1, 0, 1) };

            Assert.Throws<InvalidOperationException>(() => extractor.Extract(input));
        }

        private static void AssertRectangle(PlanarFace face, double length, double width, double centerX, double centerY)
        {
            Rectangle2 rectangle;
            Assert.True(Rectangle2.TryCreate(face, out rectangle));
            Assert.Equal(length, rectangle.Length, 6);
            Assert.Equal(width, rectangle.Width, 6);
            Assert.Equal(centerX, rectangle.Center.X, 6);
            Assert.Equal(centerY, rectangle.Center.Y, 6);
        }

        private static string FaceSignature(PlanarFace face)
        {
            return $"{face.Centroid.X:F3}|{face.Centroid.Y:F3}|{face.Area:F3}";
        }

        private static IEnumerable<Segment2> Rectangle(double minX, double minY, double maxX, double maxY)
        {
            yield return Line(minX, minY, maxX, minY);
            yield return Line(maxX, minY, maxX, maxY);
            yield return Line(maxX, maxY, minX, maxY);
            yield return Line(minX, maxY, minX, minY);
        }

        private static Segment2 Line(double x1, double y1, double x2, double y2)
        {
            return new Segment2(new Point2(x1, y1), new Point2(x2, y2));
        }

        private static Point2 RotateTranslate(double x, double y, Point2 center, double angle)
        {
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            return new Point2(center.X + x * cos - y * sin, center.Y + x * sin + y * cos);
        }
    }
}
