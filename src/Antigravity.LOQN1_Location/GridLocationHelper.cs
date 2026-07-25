using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace LOQN1_Location_element
{
    /// <summary>
    /// Lưu trữ thông tin phân loại của một Trục Grid trong Revit.
    /// </summary>
    public class GridInfo
    {
        public string Name { get; set; }
        public double Coordinate { get; set; } // Tọa độ X (với Grid X) hoặc Y (với Grid Y)
        public Grid RevitGrid { get; set; }

        public GridInfo(string name, double coord, Grid grid)
        {
            Name = name;
            Coordinate = coord;
            RevitGrid = grid;
        }
    }

    /// <summary>
    /// Helper class chuyên xử lý thuật toán phân loại và tra cứu Grid X, Y.
    /// </summary>
    public class GridLocationHelper
    {
        public List<GridInfo> XGrids { get; private set; } = new List<GridInfo>();
        public List<GridInfo> YGrids { get; private set; } = new List<GridInfo>();

        /// <summary>
        /// Khởi tạo helper và tự động phân loại danh sách Grid trong dự án thành 2 nhóm (Grid phương X và Grid phương Y).
        /// </summary>
        public GridLocationHelper(Document doc)
        {
            InitializeGrids(doc);
        }

        private void InitializeGrids(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(Grid))
                .WhereElementIsNotElementType();

            foreach (Grid grid in collector.Cast<Grid>())
            {
                Curve curve = grid.Curve;
                if (curve == null) continue;

                // Lấy điểm bắt đầu và điểm kết thúc của Grid
                XYZ start = curve.GetEndPoint(0);
                XYZ end = curve.GetEndPoint(1);
                XYZ dir = (end - start).Normalize();

                // Nếu vector hướng chạy chủ yếu theo phương Y (|dir.Y| > |dir.X|)
                // -> Đây là Grid đứng (phân tách không gian theo trục X -> Grid phương X)
                if (Math.Abs(dir.Y) >= Math.Abs(dir.X))
                {
                    double coordX = (start.X + end.X) / 2.0;
                    XGrids.Add(new GridInfo(grid.Name, coordX, grid));
                }
                // Ngược lại, vector hướng chạy chủ yếu theo phương X (|dir.X| > |dir.Y|)
                // -> Đây là Grid ngang (phân tách không gian theo trục Y -> Grid phương Y)
                else
                {
                    double coordY = (start.Y + end.Y) / 2.0;
                    YGrids.Add(new GridInfo(grid.Name, coordY, grid));
                }
            }

            // Sắp xếp các Grid tăng dần theo tọa độ
            XGrids = XGrids.OrderBy(g => g.Coordinate).ToList();
            YGrids = YGrids.OrderBy(g => g.Coordinate).ToList();
        }

        /// <summary>
        /// Thuật toán tra cứu dải Grid (StartGrid, EndGrid) dựa trên dải tọa độ min/max của đối tượng.
        /// </summary>
        /// <param name="minVal">Tọa độ nhỏ nhất của đối tượng (X_min hoặc Y_min)</param>
        /// <param name="maxVal">Tọa độ lớn nhất của đối tượng (X_max hoặc Y_max)</param>
        /// <param name="sortedGrids">Danh sách GridInfo đã sắp xếp tăng dần</param>
        /// <returns>Tuple (Grid_Start, Grid_End)</returns>
        public (string StartGrid, string EndGrid) FindGridRange(double minVal, double maxVal, List<GridInfo> sortedGrids)
        {
            if (sortedGrids == null || sortedGrids.Count == 0)
            {
                return ("N/A", "N/A");
            }

            // Tìm Grid GẦN NHẤT với minVal (cạnh dưới/trái của đối tượng)
            int startIndex = FindNearestGridIndex(minVal, sortedGrids);

            // Tìm Grid GẦN NHẤT với maxVal (cạnh trên/phải của đối tượng)
            int endIndex = FindNearestGridIndex(maxVal, sortedGrids);

            // Đảm bảo endIndex >= startIndex
            if (endIndex < startIndex)
            {
                int temp = startIndex;
                startIndex = endIndex;
                endIndex = temp;
            }

            // Nếu cả 2 cạnh đều gần cùng 1 Grid (đối tượng nhỏ nằm gọn trong 1 ô)
            // -> Mở rộng ra Grid kế bên gần nhất
            if (startIndex == endIndex)
            {
                double midVal = (minVal + maxVal) / 2.0;
                double gridCoord = sortedGrids[startIndex].Coordinate;

                if (midVal > gridCoord && startIndex + 1 < sortedGrids.Count)
                {
                    endIndex = startIndex + 1;
                }
                else if (midVal < gridCoord && startIndex - 1 >= 0)
                {
                    startIndex = startIndex - 1;
                }
                else if (startIndex + 1 < sortedGrids.Count)
                {
                    endIndex = startIndex + 1;
                }
                else if (startIndex - 1 >= 0)
                {
                    startIndex = startIndex - 1;
                }
            }

            string startName = sortedGrids[startIndex].Name;
            string endName = sortedGrids[endIndex].Name;

            return (startName, endName);
        }

        /// <summary>
        /// Tìm index của Grid có tọa độ GẦN NHẤT với giá trị cho trước.
        /// </summary>
        private int FindNearestGridIndex(double value, List<GridInfo> sortedGrids)
        {
            int bestIndex = 0;
            double bestDistance = Math.Abs(sortedGrids[0].Coordinate - value);

            for (int i = 1; i < sortedGrids.Count; i++)
            {
                double dist = Math.Abs(sortedGrids[i].Coordinate - value);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Lấy dải GridX (GridX_Start, GridX_End) cho dải X [minX, maxX] của đối tượng.
        /// </summary>
        public (string StartGrid, string EndGrid) GetGridXRange(double minX, double maxX)
        {
            return FindGridRange(minX, maxX, XGrids);
        }

        /// <summary>
        /// Lấy dải GridY (GridY_Start, GridY_End) cho dải Y [minY, maxY] của đối tượng.
        /// </summary>
        public (string StartGrid, string EndGrid) GetGridYRange(double minY, double maxY)
        {
            return FindGridRange(minY, maxY, YGrids);
        }
    }
}
