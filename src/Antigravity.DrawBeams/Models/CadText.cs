using System;
using System.Text.RegularExpressions;

namespace Antigravity.DrawBeams.Models
{
    public class CadText
    {
        public string Id { get; set; }
        public string TextString { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Rotation { get; set; }
        public double TextHeight { get; set; }
        public string Layer { get; set; }
        public string ObjectName { get; set; }

        public string CleanText
        {
            get
            {
                if (string.IsNullOrEmpty(TextString)) return string.Empty;
                string txt = TextString;
                if (string.Equals(ObjectName, "AcDbMText", StringComparison.OrdinalIgnoreCase))
                {
                    txt = Regex.Replace(txt, @"\{[^;]*;", "");
                    txt = txt.Replace("}", "");
                    txt = Regex.Replace(txt, @"\\[PfgCLHKTQW].*?;", "");
                    txt = Regex.Replace(txt, @"\\[PfgCLHKTQW]", " ");
                }
                return txt.Trim();
            }
        }
    }
}
