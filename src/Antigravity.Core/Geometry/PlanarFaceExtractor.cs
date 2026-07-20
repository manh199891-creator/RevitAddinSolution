using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.Core.Geometry
{
    /// <summary>
    /// Dependency-free 2D planar graph polygonizer used by CAD adapters.
    /// The extractor nodes input segments, builds two half-edges per edge and
    /// returns only bounded counter-clockwise faces.
    /// </summary>
    public sealed class PlanarFaceExtractor
    {
        private readonly double _tolerance;
        private readonly double _areaTolerance;
        private readonly int _maxInputSegments;

        public PlanarFaceExtractor(double tolerance = 0.001, double areaTolerance = 1e-8, int maxInputSegments = 5000)
        {
            if (tolerance <= 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
            if (areaTolerance < 0) throw new ArgumentOutOfRangeException(nameof(areaTolerance));
            if (maxInputSegments <= 0) throw new ArgumentOutOfRangeException(nameof(maxInputSegments));

            _tolerance = tolerance;
            _areaTolerance = areaTolerance;
            _maxInputSegments = maxInputSegments;
        }

        public PlanarFaceResult Extract(IEnumerable<Segment2> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var input = source.Where(s => s.Length > _tolerance).ToList();
            if (input.Count > _maxInputSegments)
            {
                throw new InvalidOperationException(
                    $"CAD linework has {input.Count} segments; the safe limit is {_maxInputSegments}.");
            }

            var edges = NodeAndNormalize(input);
            var faces = WalkBoundedFaces(edges);
            return new PlanarFaceResult(faces, input.Count, edges.Count);
        }

        private List<NormalizedEdge> NodeAndNormalize(IList<Segment2> input)
        {
            var splitParameters = new List<double>[input.Count];
            for (var i = 0; i < input.Count; i++)
            {
                splitParameters[i] = new List<double> { 0.0, 1.0 };
            }

            // Noding is intentionally performed before graph construction. It covers
            // crossings, T-junctions and collinear partial overlaps.
            for (var i = 0; i < input.Count; i++)
            {
                for (var j = i + 1; j < input.Count; j++)
                {
                    AddPairIntersections(input[i], input[j], splitParameters[i], splitParameters[j]);
                }
            }

            var unique = new Dictionary<UndirectedEdgeKey, NormalizedEdge>();
            for (var i = 0; i < input.Count; i++)
            {
                var segment = input[i];
                var parameters = SortAndDedupe(splitParameters[i]);
                for (var p = 0; p < parameters.Count - 1; p++)
                {
                    var a = Snap(segment.PointAt(parameters[p]));
                    var b = Snap(segment.PointAt(parameters[p + 1]));
                    if (a.DistanceTo(b) <= _tolerance) continue;

                    var aKey = VertexKey.From(a, _tolerance);
                    var bKey = VertexKey.From(b, _tolerance);
                    if (aKey.Equals(bKey)) continue;

                    var key = new UndirectedEdgeKey(aKey, bKey);
                    if (!unique.ContainsKey(key))
                    {
                        unique.Add(key, new NormalizedEdge(aKey, bKey, a, b));
                    }
                }
            }

            return unique.Values
                .OrderBy(e => e.Key)
                .ToList();
        }

        private void AddPairIntersections(
            Segment2 first,
            Segment2 second,
            ICollection<double> firstParameters,
            ICollection<double> secondParameters)
        {
            var p = first.Start;
            var r = first.End - first.Start;
            var q = second.Start;
            var s = second.End - second.Start;
            var cross = Point2.Cross(r, s);
            var qMinusP = q - p;
            var scale = Math.Max(1.0, Math.Max(first.Length, second.Length));

            if (Math.Abs(cross) > _tolerance * scale)
            {
                var t = Point2.Cross(qMinusP, s) / cross;
                var u = Point2.Cross(qMinusP, r) / cross;
                if (WithinSegment(t) && WithinSegment(u))
                {
                    firstParameters.Add(Clamp01(t));
                    secondParameters.Add(Clamp01(u));
                }
                return;
            }

            // Parallel but not collinear.
            if (Math.Abs(Point2.Cross(qMinusP, r)) > _tolerance * scale) return;

            AddEndpointIfOnSegment(second.Start, first, firstParameters, 0.0, secondParameters);
            AddEndpointIfOnSegment(second.End, first, firstParameters, 1.0, secondParameters);
            AddEndpointIfOnSegment(first.Start, second, secondParameters, 0.0, firstParameters);
            AddEndpointIfOnSegment(first.End, second, secondParameters, 1.0, firstParameters);
        }

        private void AddEndpointIfOnSegment(
            Point2 point,
            Segment2 host,
            ICollection<double> hostParameters,
            double endpointParameter,
            ICollection<double> endpointOwnerParameters)
        {
            double hostParameter;
            if (!TryParameterOnSegment(point, host, out hostParameter)) return;
            hostParameters.Add(Clamp01(hostParameter));
            endpointOwnerParameters.Add(endpointParameter);
        }

        private bool TryParameterOnSegment(Point2 point, Segment2 segment, out double parameter)
        {
            var direction = segment.End - segment.Start;
            var lengthSquared = direction.LengthSquared;
            parameter = 0;
            if (lengthSquared <= _tolerance * _tolerance) return false;

            parameter = Point2.Dot(point - segment.Start, direction) / lengthSquared;
            if (!WithinSegment(parameter)) return false;

            var projected = segment.PointAt(Clamp01(parameter));
            return projected.DistanceTo(point) <= _tolerance;
        }

        private List<PlanarFace> WalkBoundedFaces(IList<NormalizedEdge> edges)
        {
            var vertices = new Dictionary<VertexKey, Vertex>();
            var halfEdges = new List<HalfEdge>(edges.Count * 2);

            foreach (var edge in edges)
            {
                Vertex from;
                if (!vertices.TryGetValue(edge.AKey, out from))
                {
                    from = new Vertex(edge.AKey, edge.A);
                    vertices.Add(edge.AKey, from);
                }

                Vertex to;
                if (!vertices.TryGetValue(edge.BKey, out to))
                {
                    to = new Vertex(edge.BKey, edge.B);
                    vertices.Add(edge.BKey, to);
                }

                var forward = new HalfEdge(from, to);
                var reverse = new HalfEdge(to, from);
                forward.Twin = reverse;
                reverse.Twin = forward;
                from.Outgoing.Add(forward);
                to.Outgoing.Add(reverse);
                halfEdges.Add(forward);
                halfEdges.Add(reverse);
            }

            foreach (var vertex in vertices.Values)
            {
                vertex.Outgoing.Sort((left, right) =>
                {
                    var angleCompare = left.Angle.CompareTo(right.Angle);
                    return angleCompare != 0 ? angleCompare : left.To.Key.CompareTo(right.To.Key);
                });
            }

            var faces = new List<PlanarFace>();
            var faceKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var start in halfEdges.OrderBy(h => h.From.Key).ThenBy(h => h.To.Key))
            {
                if (start.Visited) continue;

                var ring = new List<Point2>();
                var current = start;
                var closed = false;
                for (var steps = 0; steps <= halfEdges.Count; steps++)
                {
                    if (current.Visited && !ReferenceEquals(current, start)) break;
                    current.Visited = true;
                    ring.Add(current.From.Point);

                    var outgoing = current.To.Outgoing;
                    var incomingIndex = outgoing.IndexOf(current.Twin);
                    if (incomingIndex < 0) break;

                    // Previous edge in CCW order keeps the traversed face on the left.
                    current = outgoing[(incomingIndex - 1 + outgoing.Count) % outgoing.Count];
                    if (ReferenceEquals(current, start))
                    {
                        closed = true;
                        break;
                    }
                }

                if (!closed || ring.Count < 3) continue;
                ring = SimplifyCollinear(ring);
                if (ring.Count < 3) continue;

                var area = SignedArea(ring);
                if (area <= _areaTolerance) continue; // exterior and degenerate rings

                var key = CanonicalRingKey(ring);
                if (faceKeys.Add(key)) faces.Add(new PlanarFace(ring, area));
            }

            return faces
                .OrderBy(f => f.Centroid.X)
                .ThenBy(f => f.Centroid.Y)
                .ThenBy(f => f.Area)
                .ToList();
        }

        private List<Point2> SimplifyCollinear(IList<Point2> ring)
        {
            var result = ring.ToList();
            var changed = true;
            while (changed && result.Count > 3)
            {
                changed = false;
                for (var i = 0; i < result.Count; i++)
                {
                    var previous = result[(i - 1 + result.Count) % result.Count];
                    var current = result[i];
                    var next = result[(i + 1) % result.Count];
                    var first = current - previous;
                    var second = next - current;
                    var scale = Math.Max(1.0, Math.Max(first.Length, second.Length));
                    if (Math.Abs(Point2.Cross(first, second)) <= _tolerance * scale &&
                        Point2.Dot(first, second) >= 0)
                    {
                        result.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }
            }
            return result;
        }

        private string CanonicalRingKey(IList<Point2> ring)
        {
            var keys = ring.Select(p => VertexKey.From(p, _tolerance)).ToList();
            var best = 0;
            for (var i = 1; i < keys.Count; i++)
            {
                if (CompareRotation(keys, i, best) < 0) best = i;
            }
            return string.Join(";", Enumerable.Range(0, keys.Count).Select(i => keys[(best + i) % keys.Count].ToString()));
        }

        private static int CompareRotation(IList<VertexKey> keys, int leftStart, int rightStart)
        {
            for (var i = 0; i < keys.Count; i++)
            {
                var comparison = keys[(leftStart + i) % keys.Count].CompareTo(keys[(rightStart + i) % keys.Count]);
                if (comparison != 0) return comparison;
            }
            return 0;
        }

        private List<double> SortAndDedupe(IEnumerable<double> parameters)
        {
            var ordered = parameters.Select(Clamp01).OrderBy(value => value).ToList();
            var result = new List<double>();
            foreach (var value in ordered)
            {
                if (result.Count == 0 || Math.Abs(value - result[result.Count - 1]) > 1e-10)
                {
                    result.Add(value);
                }
            }
            return result;
        }

        private Point2 Snap(Point2 point)
        {
            var key = VertexKey.From(point, _tolerance);
            return new Point2(key.X * _tolerance, key.Y * _tolerance);
        }

        private bool WithinSegment(double value)
        {
            return value >= -_tolerance && value <= 1.0 + _tolerance;
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static double SignedArea(IList<Point2> ring)
        {
            var twiceArea = 0.0;
            for (var i = 0; i < ring.Count; i++)
            {
                var next = ring[(i + 1) % ring.Count];
                twiceArea += ring[i].X * next.Y - next.X * ring[i].Y;
            }
            return twiceArea / 2.0;
        }

        private sealed class Vertex
        {
            public Vertex(VertexKey key, Point2 point)
            {
                Key = key;
                Point = point;
                Outgoing = new List<HalfEdge>();
            }

            public VertexKey Key { get; }
            public Point2 Point { get; }
            public List<HalfEdge> Outgoing { get; }
        }

        private sealed class HalfEdge
        {
            public HalfEdge(Vertex from, Vertex to)
            {
                From = from;
                To = to;
                Angle = Math.Atan2(to.Point.Y - from.Point.Y, to.Point.X - from.Point.X);
            }

            public Vertex From { get; }
            public Vertex To { get; }
            public double Angle { get; }
            public HalfEdge Twin { get; set; }
            public bool Visited { get; set; }
        }

        private sealed class NormalizedEdge
        {
            public NormalizedEdge(VertexKey aKey, VertexKey bKey, Point2 a, Point2 b)
            {
                if (aKey.CompareTo(bKey) <= 0)
                {
                    AKey = aKey;
                    BKey = bKey;
                    A = a;
                    B = b;
                }
                else
                {
                    AKey = bKey;
                    BKey = aKey;
                    A = b;
                    B = a;
                }
                Key = new UndirectedEdgeKey(AKey, BKey);
            }

            public VertexKey AKey { get; }
            public VertexKey BKey { get; }
            public Point2 A { get; }
            public Point2 B { get; }
            public UndirectedEdgeKey Key { get; }
        }

        private struct VertexKey : IEquatable<VertexKey>, IComparable<VertexKey>
        {
            public VertexKey(long x, long y)
            {
                X = x;
                Y = y;
            }

            public long X { get; }
            public long Y { get; }

            public static VertexKey From(Point2 point, double tolerance)
            {
                return new VertexKey(
                    (long)Math.Round(point.X / tolerance, MidpointRounding.AwayFromZero),
                    (long)Math.Round(point.Y / tolerance, MidpointRounding.AwayFromZero));
            }

            public int CompareTo(VertexKey other)
            {
                var xCompare = X.CompareTo(other.X);
                return xCompare != 0 ? xCompare : Y.CompareTo(other.Y);
            }

            public bool Equals(VertexKey other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is VertexKey && Equals((VertexKey)obj);
            public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
            public override string ToString() => X + "," + Y;
        }

        private struct UndirectedEdgeKey : IEquatable<UndirectedEdgeKey>, IComparable<UndirectedEdgeKey>
        {
            public UndirectedEdgeKey(VertexKey first, VertexKey second)
            {
                if (first.CompareTo(second) <= 0)
                {
                    A = first;
                    B = second;
                }
                else
                {
                    A = second;
                    B = first;
                }
            }

            public VertexKey A { get; }
            public VertexKey B { get; }

            public int CompareTo(UndirectedEdgeKey other)
            {
                var first = A.CompareTo(other.A);
                return first != 0 ? first : B.CompareTo(other.B);
            }

            public bool Equals(UndirectedEdgeKey other) => A.Equals(other.A) && B.Equals(other.B);
            public override bool Equals(object obj) => obj is UndirectedEdgeKey && Equals((UndirectedEdgeKey)obj);
            public override int GetHashCode() => unchecked((A.GetHashCode() * 397) ^ B.GetHashCode());
        }
    }

    public struct Point2 : IEquatable<Point2>
    {
        public Point2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
        public double Length => Math.Sqrt(LengthSquared);
        public double LengthSquared => X * X + Y * Y;

        public double DistanceTo(Point2 other) => (this - other).Length;
        public static double Dot(Point2 left, Point2 right) => left.X * right.X + left.Y * right.Y;
        public static double Cross(Point2 left, Point2 right) => left.X * right.Y - left.Y * right.X;
        public static Point2 operator +(Point2 left, Point2 right) => new Point2(left.X + right.X, left.Y + right.Y);
        public static Point2 operator -(Point2 left, Point2 right) => new Point2(left.X - right.X, left.Y - right.Y);
        public static Point2 operator *(Point2 point, double factor) => new Point2(point.X * factor, point.Y * factor);
        public bool Equals(Point2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Point2 && Equals((Point2)obj);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
    }

    public struct Segment2
    {
        public Segment2(Point2 start, Point2 end)
        {
            Start = start;
            End = end;
        }

        public Point2 Start { get; }
        public Point2 End { get; }
        public double Length => Start.DistanceTo(End);
        public Point2 PointAt(double parameter) => Start + (End - Start) * parameter;
    }

    public sealed class PlanarFace
    {
        internal PlanarFace(IList<Point2> vertices, double area)
        {
            Vertices = vertices.ToList().AsReadOnly();
            Area = area;
            Centroid = CalculateCentroid(vertices, area);
        }

        public IReadOnlyList<Point2> Vertices { get; }
        public double Area { get; }
        public Point2 Centroid { get; }

        private static Point2 CalculateCentroid(IList<Point2> vertices, double area)
        {
            var x = 0.0;
            var y = 0.0;
            for (var i = 0; i < vertices.Count; i++)
            {
                var next = vertices[(i + 1) % vertices.Count];
                var cross = Point2.Cross(vertices[i], next);
                x += (vertices[i].X + next.X) * cross;
                y += (vertices[i].Y + next.Y) * cross;
            }
            var factor = 1.0 / (6.0 * area);
            return new Point2(x * factor, y * factor);
        }
    }

    public sealed class PlanarFaceResult
    {
        internal PlanarFaceResult(IReadOnlyList<PlanarFace> faces, int inputSegmentCount, int normalizedEdgeCount)
        {
            Faces = faces;
            InputSegmentCount = inputSegmentCount;
            NormalizedEdgeCount = normalizedEdgeCount;
        }

        public IReadOnlyList<PlanarFace> Faces { get; }
        public int InputSegmentCount { get; }
        public int NormalizedEdgeCount { get; }
    }

    public sealed class Rectangle2
    {
        private Rectangle2(Point2 center, double length, double width, double rotationRadians)
        {
            Center = center;
            Length = length;
            Width = width;
            RotationRadians = rotationRadians;
        }

        public Point2 Center { get; }
        public double Length { get; }
        public double Width { get; }
        public double RotationRadians { get; }

        public static bool TryCreate(
            PlanarFace face,
            out Rectangle2 rectangle,
            double distanceTolerance = 0.001,
            double angularDotTolerance = 0.01)
        {
            rectangle = null;
            if (face == null || face.Vertices.Count != 4) return false;

            var vertices = face.Vertices;
            var edges = new Point2[4];
            var lengths = new double[4];
            for (var i = 0; i < 4; i++)
            {
                edges[i] = vertices[(i + 1) % 4] - vertices[i];
                lengths[i] = edges[i].Length;
                if (lengths[i] <= distanceTolerance) return false;
            }

            for (var i = 0; i < 4; i++)
            {
                var next = (i + 1) % 4;
                var normalizedDot = Point2.Dot(edges[i], edges[next]) / (lengths[i] * lengths[next]);
                if (Math.Abs(normalizedDot) > angularDotTolerance) return false;
            }

            if (Math.Abs(lengths[0] - lengths[2]) > distanceTolerance ||
                Math.Abs(lengths[1] - lengths[3]) > distanceTolerance)
            {
                return false;
            }

            var longestIndex = Enumerable.Range(0, 4)
                .OrderByDescending(i => lengths[i])
                .ThenBy(i => CanonicalAngle(edges[i]))
                .First();
            var direction = edges[longestIndex] * (1.0 / lengths[longestIndex]);
            if (direction.X < -distanceTolerance ||
                (Math.Abs(direction.X) <= distanceTolerance && direction.Y < 0))
            {
                direction = direction * -1.0;
            }

            rectangle = new Rectangle2(
                face.Centroid,
                lengths[longestIndex],
                lengths[(longestIndex + 1) % 4],
                Math.Atan2(direction.Y, direction.X));
            return true;
        }

        private static double CanonicalAngle(Point2 edge)
        {
            var angle = Math.Atan2(edge.Y, edge.X);
            if (angle < 0) angle += Math.PI;
            if (angle >= Math.PI) angle -= Math.PI;
            return angle;
        }
    }
}
