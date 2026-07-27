using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.DrawBeams.Services
{
    public class BeamPairCorridorService
    {
        public static (List<CadDimensionText> Accepted, List<CadDimensionText> Rejected) FindDimensionTextsInsidePairCorridor(
            CadLineSegment lineA,
            CadLineSegment lineB,
            IEnumerable<dynamic> allTexts,
            BeamPairOptions options = null)
        {
            var opt = options ?? new BeamPairOptions();
            var accepted = new List<CadDimensionText>();
            var rejected = new List<CadDimensionText>();

            if (lineA == null || lineB == null || allTexts == null)
            {
                return (accepted, rejected);
            }

            double measuredW = GetPerpendicularDistance(lineA.StartPoint, lineA.EndPoint, lineB.StartPoint);
            double dirX = lineA.DirectionX / lineA.Length;
            double dirY = lineA.DirectionY / lineA.Length;
            double normX = -dirY;
            double normY = dirX;

            // Project start/end of line A and line B onto line A vector
            double projAStart = 0;
            double projAEnd = lineA.Length;

            double projBStart = (lineB.StartPoint[0] - lineA.StartPoint[0]) * dirX + (lineB.StartPoint[1] - lineA.StartPoint[1]) * dirY;
            double projBEnd = (lineB.EndPoint[0] - lineA.StartPoint[0]) * dirX + (lineB.EndPoint[1] - lineA.StartPoint[1]) * dirY;

            double overlapStart = Math.Max(Math.Min(projAStart, projAEnd), Math.Min(projBStart, projBEnd));
            double overlapEnd = Math.Min(Math.Max(projAStart, projAEnd), Math.Max(projBStart, projBEnd));

            double minProj = overlapStart - opt.PairTextProjectionMarginMm;
            double maxProj = overlapEnd + opt.PairTextProjectionMarginMm;

            foreach (dynamic textObj in allTexts)
            {
                var parsedText = textObj is CadDimensionText ? (CadDimensionText)textObj : ParseCadDimensionText(textObj);
                if (parsedText == null) continue;

                double textX = parsedText.X;
                double textY = parsedText.Y;

                // Projection along beam axis
                double textProj = (textX - lineA.StartPoint[0]) * dirX + (textY - lineA.StartPoint[1]) * dirY;
                // Lateral perpendicular distance from line A
                double textLateral = (textX - lineA.StartPoint[0]) * normX + (textY - lineA.StartPoint[1]) * normY;
                double bLateral = (lineB.StartPoint[0] - lineA.StartPoint[0]) * normX + (lineB.StartPoint[1] - lineA.StartPoint[1]) * normY;

                double minLat = Math.Min(0, bLateral) - opt.PairTextLateralMarginMm;
                double maxLat = Math.Max(0, bLateral) + opt.PairTextLateralMarginMm;

                bool isProjInside = textProj >= minProj && textProj <= maxProj;
                bool isLateralInside = textLateral >= minLat && textLateral <= maxLat;

                if (isProjInside && isLateralInside)
                {
                    if (parsedText.Width > 0 && Math.Abs(parsedText.Width - measuredW) / parsedText.Width <= 0.30)
                    {
                        accepted.Add(parsedText);
                    }
                    else
                    {
                        rejected.Add(parsedText);
                    }
                }
                else if (isProjInside || (textLateral >= minLat - 200 && textLateral <= maxLat + 200))
                {
                    rejected.Add(parsedText);
                }
            }

            return (accepted, rejected);
        }

        public static BeamPairCorridor EvaluatePairCorridor(
            CadLineSegment lineA,
            CadLineSegment lineB,
            List<CadLineSegment> allSegments,
            IEnumerable<dynamic> allTexts,
            BeamPairOptions options = null,
            Dictionary<string, List<(CadLineSegment Partner, double Distance)>> nearestNeighbors = null)
        {
            var opt = options ?? new BeamPairOptions();

            double measuredW = GetPerpendicularDistance(lineA.StartPoint, lineA.EndPoint, lineB.StartPoint);
            double overlapLen = GetSegmentOverlapLength(lineA, lineB);
            double minLen = Math.Min(lineA.Length, lineB.Length);
            double overlapRatio = minLen > 0 ? overlapLen / minLen : 0;

            double endpointMismatch = CalculateEndpointMismatch(lineA, lineB);

            // Intervening parallel line count
            int interveningCount = CountInterveningParallelLines(lineA, lineB, allSegments, measuredW);

            // Nearest partner check
            bool isNearestA = CheckIsNearestPartner(lineA, lineB, nearestNeighbors, allSegments);
            bool isNearestB = CheckIsNearestPartner(lineB, lineA, nearestNeighbors, allSegments);

            double distNearestA = GetNearestPartnerDistance(lineA, nearestNeighbors, allSegments);
            double distNearestB = GetNearestPartnerDistance(lineB, nearestNeighbors, allSegments);

            // Corridor texts
            var (acceptedTexts, rejectedTexts) = FindDimensionTextsInsidePairCorridor(lineA, lineB, allTexts, opt);

            var corridor = new BeamPairCorridor
            {
                LineAId = lineA.Id,
                LineBId = lineB.Id,
                MeasuredWidth = measuredW,
                OverlapLength = overlapLen,
                OverlapRatio = overlapRatio,
                EndpointMismatch = endpointMismatch,
                InterveningParallelLineCount = interveningCount,
                DistanceToNearestPartnerA = distNearestA,
                DistanceToNearestPartnerB = distNearestB,
                IsNearestPartnerForA = isNearestA,
                IsNearestPartnerForB = isNearestB,
                AcceptedCorridorTexts = acceptedTexts,
                RejectedOutsideTexts = rejectedTexts,
                HasStrongCorridorText = acceptedTexts.Any(t => t.Width > 0 && Math.Abs(t.Width - measuredW) / t.Width <= 0.30)
            };

            EvaluateCorridorDecision(corridor, lineA, lineB, opt);
            return corridor;
        }

        private static void EvaluateCorridorDecision(
            BeamPairCorridor corridor,
            CadLineSegment lineA,
            CadLineSegment lineB,
            BeamPairOptions opt)
        {
            if (corridor.OverlapRatio < opt.MinimumPairOverlapRatio)
            {
                corridor.Decision = BeamPairDecisionReason.RejectedLowOverlap;
                corridor.DecisionReasonText = $"Overlap ratio {corridor.OverlapRatio:F2} below minimum {opt.MinimumPairOverlapRatio:F2}";
                corridor.Score = CalculateScore(corridor, lineA, lineB);
                return;
            }

            if (corridor.EndpointMismatch > opt.MaximumEndpointMismatchMm)
            {
                corridor.Decision = BeamPairDecisionReason.RejectedLowOverlap;
                corridor.DecisionReasonText = $"Endpoint mismatch {corridor.EndpointMismatch:F0}mm exceeds maximum {opt.MaximumEndpointMismatchMm:F0}mm";
                corridor.Score = CalculateScore(corridor, lineA, lineB);
                return;
            }

            if (corridor.InterveningParallelLineCount > 0 && !corridor.HasStrongCorridorText)
            {
                corridor.Decision = BeamPairDecisionReason.RejectedInterveningParallelLine;
                corridor.DecisionReasonText = $"Intervening parallel line count = {corridor.InterveningParallelLineCount}";
                corridor.Score = CalculateScore(corridor, lineA, lineB);
                return;
            }

            if (!corridor.IsMutualNearest && !corridor.HasStrongCorridorText)
            {
                corridor.Decision = BeamPairDecisionReason.RejectedNonMutualPair;
                corridor.DecisionReasonText = "Non-mutual nearest pair without strong confirming corridor text";
                corridor.Score = CalculateScore(corridor, lineA, lineB);
                return;
            }

            corridor.Score = CalculateScore(corridor, lineA, lineB);

            if (corridor.HasStrongCorridorText)
            {
                corridor.Decision = BeamPairDecisionReason.AcceptedCorridorText;
                corridor.DecisionReasonText = "Confirmed by strong corridor dimension text";
            }
            else
            {
                corridor.Decision = BeamPairDecisionReason.AcceptedMutualNearest;
                corridor.DecisionReasonText = "Accepted as mutual nearest parallel pair with clean geometry";
            }
        }

        private static double CalculateScore(BeamPairCorridor corridor, CadLineSegment lineA, CadLineSegment lineB)
        {
            double score = 0;

            if (corridor.AcceptedCorridorTexts.Any())
            {
                score += 1000;
                var bestText = corridor.AcceptedCorridorTexts.First();
                if (bestText.Width > 0 && Math.Abs(bestText.Width - corridor.MeasuredWidth) / bestText.Width <= 0.10)
                {
                    score += 500;
                }
            }
            else if (corridor.RejectedOutsideTexts.Any())
            {
                score -= 600;
            }

            if (corridor.IsMutualNearest)
            {
                score += 250;
            }

            if (string.Equals(lineA.Layer, lineB.Layer, StringComparison.OrdinalIgnoreCase))
            {
                score += 150;
            }

            score += corridor.OverlapRatio * 300.0;

            if (corridor.InterveningParallelLineCount > 0)
            {
                score -= 1000;
            }

            if (corridor.EndpointMismatch > 150)
            {
                score -= 400;
            }

            var text = corridor.AcceptedCorridorTexts.FirstOrDefault();
            if (text != null && text.Width > 0 && Math.Abs(text.Width - corridor.MeasuredWidth) / text.Width > 0.20)
            {
                score -= 300;
            }

            return score;
        }

        private static int CountInterveningParallelLines(
            CadLineSegment lineA,
            CadLineSegment lineB,
            List<CadLineSegment> allSegments,
            double measuredWidth)
        {
            if (allSegments == null || allSegments.Count <= 2 || measuredWidth <= 10) return 0;

            int count = 0;
            double dirAx = lineA.DirectionX / lineA.Length;
            double dirAy = lineA.DirectionY / lineA.Length;

            foreach (var seg in allSegments)
            {
                if (seg.Id == lineA.Id || seg.Id == lineB.Id) continue;
                if (seg.Length < 200) continue;

                double dot = Math.Abs((lineA.DirectionX * seg.DirectionX + lineA.DirectionY * seg.DirectionY) / (lineA.Length * seg.Length));
                if (dot < 0.999) continue;

                double distFromA = GetPerpendicularDistance(lineA.StartPoint, lineA.EndPoint, seg.StartPoint);
                if (distFromA > 15.0 && distFromA < measuredWidth - 15.0)
                {
                    double overlap = GetSegmentOverlapLength(lineA, seg);
                    if (overlap >= 150.0)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool CheckIsNearestPartner(
            CadLineSegment lineA,
            CadLineSegment lineB,
            Dictionary<string, List<(CadLineSegment Partner, double Distance)>> nearestNeighbors,
            List<CadLineSegment> allSegments)
        {
            double distAB = GetPerpendicularDistance(lineA.StartPoint, lineA.EndPoint, lineB.StartPoint);

            if (nearestNeighbors != null && nearestNeighbors.TryGetValue(lineA.Id, out var partners) && partners.Count > 0)
            {
                return Math.Abs(partners[0].Distance - distAB) <= 5.0 || partners[0].Partner.Id == lineB.Id;
            }

            // Fallback calculation
            double minDist = double.MaxValue;
            foreach (var seg in allSegments)
            {
                if (seg.Id == lineA.Id) continue;
                if (seg.Length < 200) continue;

                double dot = Math.Abs((lineA.DirectionX * seg.DirectionX + lineA.DirectionY * seg.DirectionY) / (lineA.Length * seg.Length));
                if (dot < 0.999) continue;

                double overlap = GetSegmentOverlapLength(lineA, seg);
                if (overlap < 150.0) continue;

                double d = GetPerpendicularDistance(lineA.StartPoint, lineA.EndPoint, seg.StartPoint);
                if (d >= 50.0 && d <= 2000.0 && d < minDist)
                {
                    minDist = d;
                }
            }

            return Math.Abs(minDist - distAB) <= 5.0;
        }

        private static double GetNearestPartnerDistance(
            CadLineSegment lineA,
            Dictionary<string, List<(CadLineSegment Partner, double Distance)>> nearestNeighbors,
            List<CadLineSegment> allSegments)
        {
            if (nearestNeighbors != null && nearestNeighbors.TryGetValue(lineA.Id, out var partners) && partners.Count > 0)
            {
                return partners[0].Distance;
            }
            return 0.0;
        }

        private static double CalculateEndpointMismatch(CadLineSegment lineA, CadLineSegment lineB)
        {
            double dirX = lineA.DirectionX / lineA.Length;
            double dirY = lineA.DirectionY / lineA.Length;

            double pAStart = 0;
            double pAEnd = lineA.Length;

            double pBStart = (lineB.StartPoint[0] - lineA.StartPoint[0]) * dirX + (lineB.StartPoint[1] - lineA.StartPoint[1]) * dirY;
            double pBEnd = (lineB.EndPoint[0] - lineA.StartPoint[0]) * dirX + (lineB.EndPoint[1] - lineA.StartPoint[1]) * dirY;

            double minB = Math.Min(pBStart, pBEnd);
            double maxB = Math.Max(pBStart, pBEnd);

            double diffStart = Math.Abs(pAStart - minB);
            double diffEnd = Math.Abs(pAEnd - maxB);

            return Math.Max(diffStart, diffEnd);
        }

        public static double GetPerpendicularDistance(double[] p1, double[] p2, double[] pt)
        {
            double dx = p2[0] - p1[0];
            double dy = p2[1] - p1[1];
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return 0;
            return Math.Abs(dy * pt[0] - dx * pt[1] + p2[0] * p1[1] - p2[1] * p1[0]) / len;
        }

        public static double GetSegmentOverlapLength(CadLineSegment s1, CadLineSegment s2)
        {
            double dx = s1.DirectionX / s1.Length;
            double dy = s1.DirectionY / s1.Length;

            double p1 = 0;
            double p2 = s1.Length;

            double p3 = (s2.StartPoint[0] - s1.StartPoint[0]) * dx + (s2.StartPoint[1] - s1.StartPoint[1]) * dy;
            double p4 = (s2.EndPoint[0] - s1.StartPoint[0]) * dx + (s2.EndPoint[1] - s1.StartPoint[1]) * dy;

            double min1 = Math.Min(p1, p2), max1 = Math.Max(p1, p2);
            double min2 = Math.Min(p3, p4), max2 = Math.Max(p3, p4);

            double start = Math.Max(min1, min2);
            double end = Math.Min(max1, max2);

            return Math.Max(0, end - start);
        }

        public static CadDimensionText ParseCadDimensionText(object entity)
        {
            if (entity == null) return null;
            if (entity is CadDimensionText cdt) return cdt;

            try
            {
                string text = GetPropString(entity, "TextString") ?? GetPropString(entity, "Content");
                if (string.IsNullOrEmpty(text)) return null;

                double[] pos = GetPropPoint(entity, "InsertionPoint") ?? GetPropPoint(entity, "TextAlignmentPoint");
                if (pos == null)
                {
                    double? x = GetPropDouble(entity, "X");
                    double? y = GetPropDouble(entity, "Y");
                    if (x.HasValue && y.HasValue) pos = new double[] { x.Value, y.Value, 0 };
                }
                if (pos == null) return null;

                double rot = GetPropDouble(entity, "Rotation") ?? 0.0;

                text = System.Text.RegularExpressions.Regex.Replace(text, @"\\A\d+;", "");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\{[^}]*\}", "");
                text = text.Replace("\r", "").Replace("\n", " ").Trim();

                var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)\s*[xX*×]\s*(\d+)");
                if (match.Success)
                {
                    double w = double.Parse(match.Groups[1].Value);
                    double h = double.Parse(match.Groups[2].Value);
                    return new CadDimensionText
                    {
                        Content = text,
                        Width = w,
                        Height = h,
                        X = pos[0],
                        Y = pos[1],
                        Rotation = rot
                    };
                }
            }
            catch { }
            return null;
        }

        private static string GetPropString(object obj, string propName)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propName);
                if (prop != null) return prop.GetValue(obj)?.ToString();
                dynamic d = obj;
                return (string)d.TextString;
            }
            catch { }
            return null;
        }

        private static double[] GetPropPoint(object obj, string propName)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propName);
                if (prop != null)
                {
                    var val = prop.GetValue(obj);
                    if (val is double[] arr) return arr;
                }
                dynamic d = obj;
                var res = d.InsertionPoint;
                if (res is double[] darr) return darr;
            }
            catch { }
            return null;
        }

        private static double? GetPropDouble(object obj, string propName)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propName);
                if (prop != null)
                {
                    var val = prop.GetValue(obj);
                    if (val != null) return Convert.ToDouble(val);
                }
                dynamic d = obj;
                return Convert.ToDouble(d.Rotation);
            }
            catch { }
            return null;
        }
    }
}
