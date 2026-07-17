namespace Antigravity.Autojoin.Models
{
    /// <summary>
    /// Quy tắc join giữa hai loại phần tử (pair-based).
    /// CategoryA = phần tử ưu tiên (nét liền, cắt xuyên).
    /// CategoryB = phần tử bị cắt.
    /// Nếu SwapPriority = true → đảo: B cắt A.
    /// </summary>
    public class JoinRule
    {
        public bool   IsEnabled    { get; set; } = true;
        public string CategoryA    { get; set; }           // e.g. "OST_StructuralFraming"
        public string CategoryB    { get; set; }           // e.g. "OST_Floors"

        /// <summary>Thứ tự hiển thị trong UI (1-based). Tính lại sau reorder.</summary>
        public int    Order        { get; set; }

        // ── Display helpers ──

        public string DisplayName => $"{FriendlyName(CategoryA)} → {FriendlyName(CategoryB)}";
        public string NameA       => FriendlyName(CategoryA);
        public string NameB       => FriendlyName(CategoryB);
        public string OrderLabel  => $"#{Order}";

        public static string FriendlyName(string builtInCategory)
        {
            switch (builtInCategory)
            {
                case "OST_StructuralFraming":  return "Structural Framing";
                case "OST_Floors":             return "Floors";
                case "OST_StructuralColumns":  return "Structural Columns";
                case "OST_Walls":              return "Structural Walls";
                case "OST_Toposolid":          return "Toposolid";
                case "OST_StructuralFoundation": return "Structural Foundations";
                default:                       return builtInCategory ?? "";
            }
        }

        /// <summary>Danh sách category hỗ trợ – dùng cho ComboBox.</summary>
        public static readonly string[] SupportedCategories = new[]
        {
            "OST_StructuralColumns",
            "OST_StructuralFraming",
            "OST_Walls",
            "OST_Floors",
            "OST_Toposolid",
            "OST_StructuralFoundation"
        };
    }
}
