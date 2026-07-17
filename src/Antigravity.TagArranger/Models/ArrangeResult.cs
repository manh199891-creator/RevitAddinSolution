using System.Collections.Generic;

namespace Antigravity.TagArranger.Models
{
    /// <summary>
    /// Kết quả sau khi thực hiện thao tác sắp xếp.
    /// </summary>
    public class ArrangeResult
    {
        /// <summary>Tổng số annotation đã xử lý.</summary>
        public int ProcessedCount { get; set; }

        /// <summary>Số annotation đã thực sự di chuyển.</summary>
        public int MovedCount { get; set; }

        /// <summary>Số annotation bỏ qua (không adjustable hoặc không cần di chuyển).</summary>
        public int SkippedCount { get; set; }

        /// <summary>Thông báo tổng hợp cho UI.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Chi tiết log cho debug.</summary>
        public List<string> LogMessages { get; set; } = new List<string>();
    }
}
