using System.Collections.Generic;
using System.Data;
using System.IO;
using ExcelDataReader;

namespace Antigravity.ArchModeling.Models
{
    public class ExcelMappingRow
    {
        public string CadPattern { get; set; }
        public string RevitFamily { get; set; }
        public string RevitType { get; set; }
        public string TypeMark { get; set; }
    }
}

namespace Antigravity.ArchModeling.Services
{
    using Antigravity.ArchModeling.Models;

    public class MappingExcelService
    {
        /// <summary>
        /// Reads mapping from XLSX file with dynamic column detection.
        /// Searches for keywords like 'THIET BI', 'FAMILY', 'TYPE', 'MARK' in headers.
        /// </summary>
        public List<ExcelMappingRow> ReadMappingFromFile(string path)
        {
            var result = new List<ExcelMappingRow>();
            if (!File.Exists(path)) return result;

            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var conf = new ExcelDataSetConfiguration
                    {
                        ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
                    };
                    var dataSet = reader.AsDataSet(conf);
                    if (dataSet.Tables.Count == 0) return result;
                    
                    var table = dataSet.Tables[0];
                    int rows = table.Rows.Count;
                    int cols = table.Columns.Count;

                    int colCad = -1, colFamily = -1, colType = -1, colTypeMark = -1;
                    int dataStartRow = -1;

                    for (int r = 0; r < System.Math.Min(10, rows); r++)
                    {
                        for (int c = 0; c < cols; c++)
                        {
                            string val = table.Rows[r][c]?.ToString()?.ToUpperInvariant().Trim();
                            if (string.IsNullOrEmpty(val)) continue;

                            if (val.Contains("THIET BI") || val.Contains("THIẾT BỊ") || val.Contains("CAD") || val.Contains("BLOCK")) colCad = c;
                            else if (val.Contains("FAMILY")) colFamily = c;
                            else if (val.Contains("TYPE MARK")) colTypeMark = c;
                            else if (val.Contains("TYPE")) colType = c; 
                        }

                        if (colCad != -1 && (colFamily != -1 || colType != -1 || colTypeMark != -1))
                        {
                            dataStartRow = r + 1;
                            break;
                        }
                    }

                    if (dataStartRow == -1)
                    {
                        colCad = 0; colFamily = 1; colType = 2; colTypeMark = 3;
                        dataStartRow = 1;
                    }

                    for (int r = dataStartRow; r < rows; r++)
                    {
                        string cadPattern = colCad >= 0 && colCad < cols ? table.Rows[r][colCad]?.ToString()?.Trim() : null;
                        string revitFamily = colFamily >= 0 && colFamily < cols ? table.Rows[r][colFamily]?.ToString()?.Trim() : null;
                        string revitType = colType >= 0 && colType < cols ? table.Rows[r][colType]?.ToString()?.Trim() : null;
                        string typeMark = colTypeMark >= 0 && colTypeMark < cols ? table.Rows[r][colTypeMark]?.ToString()?.Trim() : null;

                        if (string.IsNullOrWhiteSpace(cadPattern)) continue;
                        if (cadPattern.ToUpperInvariant().Contains("THIET BI")) continue;

                        result.Add(new ExcelMappingRow
                        {
                            CadPattern  = cadPattern,
                            RevitFamily = revitFamily,
                            RevitType   = revitType,
                            TypeMark    = typeMark
                        });
                    }
                }
            }
            return result;
        }
    }
}
