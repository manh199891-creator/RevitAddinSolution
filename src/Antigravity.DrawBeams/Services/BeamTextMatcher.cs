using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Antigravity.DrawBeams.Services
{
    public class BeamTextMatcher
    {
        private readonly BeamGeometryService _geometryService;

        public BeamTextMatcher(BeamGeometryService geometryService = null)
        {
            _geometryService = geometryService ?? new BeamGeometryService();
        }

        public List<BeamTextMatch> FindMatches(CadSegment anchor, IEnumerable<CadText> texts)
        {
            var matches = new List<BeamTextMatch>();
            if (anchor == null || texts == null || anchor.Length < 1e-9) return matches;

            double searchRadius = 5000.0;
            double anchorAngle = anchor.Angle;

            foreach (var txt in texts)
            {
                if (txt == null) continue;
                string content = txt.CleanText;
                if (string.IsNullOrEmpty(content)) continue;

                var dimMatch = Regex.Match(content, @"(\d+[\.,]?\d*)\s*[xX\*\-\/]\s*(\d+[\.,]?\d*)", RegexOptions.IgnoreCase);
                if (!dimMatch.Success) continue;

                string val1 = dimMatch.Groups[1].Value.Replace(',', '.');
                string val2 = dimMatch.Groups[2].Value.Replace(',', '.');

                if (!double.TryParse(val1, NumberStyles.Any, CultureInfo.InvariantCulture, out double b) ||
                    !double.TryParse(val2, NumberStyles.Any, CultureInfo.InvariantCulture, out double h))
                {
                    continue;
                }

                if (b < 100) b *= 10;
                if (h < 100) h *= 10;
                if (b <= 0 || h <= 0) continue;

                double distToLine = _geometryService.GetPerpendicularDistance(anchor, txt.X, txt.Y);
                double dynamicRadius = Math.Max(searchRadius, b * 3.0);
                if (distToLine > dynamicRadius) continue;

                double textRotation = txt.Rotation;
                while (textRotation < 0) textRotation += Math.PI;
                while (textRotation >= Math.PI) textRotation -= Math.PI;

                double angleDiff = Math.Abs(anchorAngle - textRotation);
                while (angleDiff > Math.PI / 2.0) angleDiff = Math.PI - angleDiff;
                angleDiff = Math.Abs(angleDiff);

                if (angleDiff >= Math.PI / 9.0) continue;

                double len = anchor.Length;
                double ux = anchor.DirectionX / len;
                double uy = anchor.DirectionY / len;
                double proj = ((txt.X - anchor.StartX) * ux + (txt.Y - anchor.StartY) * uy) / len;

                if (proj < -0.5 || proj > 1.5) continue;

                string mark = ExtractMark(content, dimMatch.Value);

                matches.Add(new BeamTextMatch
                {
                    Text = txt,
                    ParsedWidth = b,
                    ParsedHeight = h,
                    Content = content,
                    Mark = mark,
                    DistanceToSegment = distToLine,
                    AngleDifference = angleDiff
                });
            }

            return matches.OrderBy(m => m.DistanceToSegment).ToList();
        }

        private string ExtractMark(string textContent, string dimensionStr)
        {
            if (string.IsNullOrEmpty(textContent)) return string.Empty;

            string mark = Regex.Replace(textContent, Regex.Escape(dimensionStr), "");
            mark = Regex.Replace(mark, @"[()\[\]]", "").Trim();

            if (mark.Length > 0)
            {
                return mark.Trim();
            }

            var prefixMatch = Regex.Match(textContent, @"^(PT|RC|D\d*)\b", RegexOptions.IgnoreCase);
            if (prefixMatch.Success)
            {
                return prefixMatch.Value;
            }

            return string.Empty;
        }
    }
}
