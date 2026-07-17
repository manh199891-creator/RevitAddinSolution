using System.Collections.Generic;
using System.ComponentModel;
using Autodesk.Revit.DB;

namespace Antigravity.DrawFloors.Models
{
    /// <summary>
    /// Đại diện cho một dòng mapping: Pattern Name → Revit Floor Type + Offset.
    /// Implement INotifyPropertyChanged để DataGrid cập nhật trực quan.
    /// </summary>
    public class HatchMapping : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void Notify(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        // ── Pattern Name (readonly, từ AutoCAD) ──────────────
        private string _patternName;
        public string PatternName
        {
            get => _patternName;
            set { _patternName = value; Notify(nameof(PatternName)); }
        }

        // ── Số lượng Hatch thuộc pattern này ─────────────────
        public int HatchCount => ConnectedCadHatches?.Count ?? 0;

        // ── Loại sàn được chọn ────────────────────────────────
        private FloorType _selectedFloorType;
        public FloorType SelectedFloorType
        {
            get => _selectedFloorType;
            set { _selectedFloorType = value; Notify(nameof(SelectedFloorType)); }
        }

        // ── Độ lệch cao độ (mm) ──────────────────────────────
        private double _offsetFromLevel;
        public double OffsetFromLevel
        {
            get => _offsetFromLevel;
            set { _offsetFromLevel = value; Notify(nameof(OffsetFromLevel)); }
        }

        // ── Danh sách đối tượng Hatch thô từ AutoCAD COM ─────
        public List<object> ConnectedCadHatches { get; set; }

        public HatchMapping()
        {
            ConnectedCadHatches = new List<object>();
            OffsetFromLevel = 0.0;
        }
    }
}
