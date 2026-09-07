using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using Antigravity.IssueManager.Models;

namespace Antigravity.IssueManager.Services
{
    public static class ExcelIssueExporter
    {
        private const string MainSheetName = "Bảng kê RFI";
        private const string MetadataSheetName = "_metadata";

        private static readonly ExcelColumn[] Columns =
        {
            new ExcelColumn("no", "#", false),
            new ExcelColumn("id", "ID", false),
            new ExcelColumn("status", "Trạng thái", false),
            new ExcelColumn("priority", "Ưu tiên", false),
            new ExcelColumn("title", "Tiêu đề", false),
            new ExcelColumn("discipline", "Bộ môn", false),
            new ExcelColumn("level", "Vị trí (Khu vực/Tầng/Grid)", false),
            new ExcelColumn("creator", "Người tạo", false),
            new ExcelColumn("createdDate", "Ngày lập", false),
            new ExcelColumn("receiver", "Người nhận", false),
            new ExcelColumn("replyDue", "Hạn phản hồi", false),
            new ExcelColumn("description", "Nội dung câu hỏi", false),
            new ExcelColumn("solution", "Đề xuất giải pháp", false),
            new ExcelColumn("image3d", "Hình ảnh 3D", false),
            new ExcelColumn("image2d", "Hình ảnh 2D", false),
            new ExcelColumn("reply", "Phản hồi / Trả lời", false),
            new ExcelColumn("replyImage", "Hình ảnh phản hồi", false),
            new ExcelColumn("vvCheck", "VV kiểm tra", false),
            new ExcelColumn("checkerComment", "Ý kiến Checker", false),
            new ExcelColumn("cncComment", "Ý kiến CNC", false),
            new ExcelColumn("cdtComment", "Ý kiến CĐT", false)
        };

        public static void ExportIssues(List<IssueModel> issues, string outputFilePath)
        {
            if (issues == null) throw new ArgumentNullException(nameof(issues));
            if (string.IsNullOrWhiteSpace(outputFilePath)) throw new ArgumentException("Output file path is required.", nameof(outputFilePath));

            string outputDir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            if (File.Exists(outputFilePath))
            {
                File.Delete(outputFilePath);
            }

            // Gather valid images
            var excelImages = new List<ExcelImage>();
            int imgCounter = 1;
            for (int i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                var vp = issue.Viewpoint;
                if (vp != null)
                {
                    if (!string.IsNullOrWhiteSpace(vp.SnapshotFilePath) && File.Exists(vp.SnapshotFilePath))
                    {
                        string ext = Path.GetExtension(vp.SnapshotFilePath).TrimStart('.').ToLower();
                        if (string.IsNullOrEmpty(ext)) ext = "png";
                        excelImages.Add(new ExcelImage
                        {
                            FilePath = vp.SnapshotFilePath,
                            RowIndex = 7 + i,
                            RelId = "rId" + imgCounter,
                            ZipPath = $"xl/media/image{imgCounter}.{ext}",
                            Extension = ext,
                            IsSecond = false
                        });
                        imgCounter++;
                    }

                    if (!string.IsNullOrWhiteSpace(vp.SnapshotFilePath2))
                    {
                        var paths = vp.SnapshotFilePath2.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        var validPaths = paths.Where(p => File.Exists(p)).ToList();
                        for (int pi = 0; pi < validPaths.Count; pi++)
                        {
                            string p = validPaths[pi];
                            string ext = Path.GetExtension(p).TrimStart('.').ToLower();
                            if (string.IsNullOrEmpty(ext)) ext = "png";
                            excelImages.Add(new ExcelImage
                            {
                                FilePath = p,
                                RowIndex = 7 + i,
                                RelId = "rId" + imgCounter,
                                ZipPath = $"xl/media/image{imgCounter}.{ext}",
                                Extension = ext,
                                IsSecond = true,
                                ImageIndexInCell = pi,
                                TotalImagesInCell = validPaths.Count
                            });
                            imgCounter++;
                        }
                    }
                }
            }

            using (FileStream stream = new FileStream(outputFilePath, FileMode.CreateNew))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteTextEntry(archive, "[Content_Types].xml", BuildContentTypes(excelImages));
                WriteTextEntry(archive, "_rels/.rels", BuildRootRelationships());
                WriteTextEntry(archive, "docProps/core.xml", BuildCoreProperties());
                WriteTextEntry(archive, "docProps/app.xml", BuildAppProperties());
                WriteTextEntry(archive, "xl/workbook.xml", BuildWorkbook());
                WriteTextEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
                WriteTextEntry(archive, "xl/styles.xml", BuildStyles());
                WriteTextEntry(archive, "xl/worksheets/sheet1.xml", BuildMainSheet(issues, excelImages));
                WriteTextEntry(archive, "xl/worksheets/sheet2.xml", BuildMetadataSheet(issues));

                if (excelImages.Count > 0)
                {
                    WriteTextEntry(archive, "xl/worksheets/_rels/sheet1.xml.rels", BuildSheetRelationships());
                    WriteTextEntry(archive, "xl/drawings/drawing1.xml", BuildDrawingXml(excelImages));
                    WriteTextEntry(archive, "xl/drawings/_rels/drawing1.xml.rels", BuildDrawingRelationships(excelImages));

                    foreach (var img in excelImages)
                    {
                        ZipArchiveEntry imgEntry = archive.CreateEntry(img.ZipPath);
                        using (Stream entryStream = imgEntry.Open())
                        using (FileStream fileStream = new FileStream(img.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            fileStream.CopyTo(entryStream);
                        }
                    }
                }
            }
        }

        private static string BuildMainSheet(List<IssueModel> issues, List<ExcelImage> images)
        {
            bool hasDrawing = images.Count > 0;
            var rowsWithImages = new HashSet<int>(images.Select(img => img.RowIndex));

            return CreateXml(writer =>
            {
                writer.WriteStartElement("worksheet", SpreadsheetNamespace);
                writer.WriteAttributeString("xmlns", "r", null, RelationshipNamespace);

                writer.WriteStartElement("sheetViews");
                writer.WriteStartElement("sheetView");
                writer.WriteAttributeString("workbookViewId", "0");
                writer.WriteStartElement("pane");
                writer.WriteAttributeString("ySplit", "6");
                writer.WriteAttributeString("topLeftCell", "A7");
                writer.WriteAttributeString("activePane", "bottomLeft");
                writer.WriteAttributeString("state", "frozen");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();

                WriteColumns(writer);

                writer.WriteStartElement("sheetData");
                WriteRow(writer, 1, Columns.Length, new[] { "BÁO CÁO XUNG ĐỘT" }, 1, 26);
                WriteRow(writer, 2, Columns.Length, new[] { "UNDERGROUND" }, 9, 20);
                WriteRow(writer, 3, Columns.Length, new[] { "Dự án: Gamuda | Mã: GAMUDA | Nguồn: Máy chủ" }, 9, 20);
                WriteRow(writer, 4, Columns.Length, new[] { $"Tổng: {issues.Count} | Mở: {issues.Count} | Trả lời: 0 | Đóng/Hủy: 0" }, 9, 20);
                WriteEmptyRow(writer, 5);
                
                var row6Labels = new string[Columns.Length];
                row6Labels[0] = "Thông tin chung";
                row6Labels[5] = "Phân loại";
                row6Labels[7] = "Trách nhiệm & Thời hạn";
                row6Labels[11] = "Chi tiết vấn đề";
                row6Labels[15] = "Phản hồi & Đánh giá";
                WriteRow(writer, 6, Columns.Length, row6Labels, 10, 20);
                
                WriteRow(writer, 7, Columns.Length, Columns.Select(c => c.Label).ToArray(), 3, 28);

                for (int i = 0; i < issues.Count; i++)
                {
                    int rowIndex = 8 + i;
                    bool clearImageCol = rowsWithImages.Contains(rowIndex - 1);
                    double height = clearImageCol ? 120.0 : 64.0;
                    WriteRow(writer, rowIndex, Columns.Length, BuildIssueRow(issues[i], i + 1, clearImageCol), 4, height);
                }

                writer.WriteEndElement();

                writer.WriteStartElement("autoFilter");
                writer.WriteAttributeString("ref", $"A7:U{Math.Max(8, issues.Count + 7)}");
                writer.WriteEndElement();

                writer.WriteStartElement("mergeCells");
                writer.WriteAttributeString("count", "9");
                WriteMergeCell(writer, "A1:U1");
                WriteMergeCell(writer, "A2:U2");
                WriteMergeCell(writer, "A3:U3");
                WriteMergeCell(writer, "A4:U4");
                WriteMergeCell(writer, "A6:E6");
                WriteMergeCell(writer, "F6:G6");
                WriteMergeCell(writer, "H6:K6");
                WriteMergeCell(writer, "L6:O6");
                WriteMergeCell(writer, "P6:U6");
                writer.WriteEndElement();

                if (hasDrawing)
                {
                    writer.WriteStartElement("drawing");
                    writer.WriteAttributeString("r", "id", RelationshipNamespace, "rId1");
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            });
        }

        private static string BuildMetadataSheet(List<IssueModel> issues)
        {
            return CreateXml(writer =>
            {
                writer.WriteStartElement("worksheet", SpreadsheetNamespace);
                WriteMetadataColumns(writer);
                writer.WriteStartElement("sheetData");

                WriteRow(writer, 1, 5, new[] { "type", "vilaiviet-rfi-register" }, 0, 18);
                WriteRow(writer, 2, 5, new[] { "version", "2" }, 0, 18);
                WriteRow(writer, 3, 5, new[] { "dataSheet", MainSheetName }, 0, 18);
                WriteRow(writer, 4, 5, new[] { "headerRow", "6" }, 0, 18);
                WriteRow(writer, 5, 5, new[] { "firstDataRow", "7" }, 0, 18);
                WriteRow(writer, 6, 5, new[] { "exportedAt", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) }, 0, 18);
                WriteEmptyRow(writer, 7);
                WriteRow(writer, 8, 5, new[] { "columns" }, 2, 18);
                WriteRow(writer, 9, 5, new[] { "index", "letter", "key", "label", "editable" }, 3, 22);

                for (int i = 0; i < Columns.Length; i++)
                {
                    ExcelColumn column = Columns[i];
                    WriteRow(writer, 10 + i, 5, new[]
                    {
                        (i + 1).ToString(CultureInfo.InvariantCulture),
                        GetColumnName(i + 1),
                        column.Key,
                        column.Label,
                        column.Editable ? "1" : "0"
                    }, 4, 18);
                }

                int rowsTitle = 11 + Columns.Length;
                WriteEmptyRow(writer, rowsTitle - 1);
                WriteRow(writer, rowsTitle, 5, new[] { "rows" }, 2, 18);
                WriteRow(writer, rowsTitle + 1, 5, new[] { "sheetRow", "issueNo", "id" }, 3, 22);

                for (int i = 0; i < issues.Count; i++)
                {
                    IssueModel issue = issues[i];
                    WriteRow(writer, rowsTitle + 2 + i, 5, new[]
                    {
                        (7 + i).ToString(CultureInfo.InvariantCulture),
                        GetIssueNumber(issue),
                        issue?.IssueId ?? string.Empty
                    }, 4, 18);
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
            });
        }

        private static string[] BuildIssueRow(IssueModel issue, int index, bool clearImageCol)
        {
            return new[]
            {
                index.ToString(CultureInfo.InvariantCulture),
                string.Empty,
                string.Empty,
                string.Empty,
                issue?.Title ?? issue?.DisplayTitle ?? string.Empty,
                string.Empty,
                issue?.Level ?? string.Empty,
                "Manhns",
                DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                issue?.AssignedTo ?? string.Empty,
                string.Empty,
                issue?.Description ?? string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty, // Phản hồi
                string.Empty, // Hình ảnh phản hồi
                string.Empty, // VV kiểm tra
                string.Empty, // Ý kiến Checker
                string.Empty, // Ý kiến CNC
                string.Empty  // Ý kiến CĐT
            };
        }

        private static string GetIssueNumber(IssueModel issue)
        {
            if (!string.IsNullOrWhiteSpace(issue?.IssueCode)) return issue.IssueCode.Trim();
            if (!string.IsNullOrWhiteSpace(issue?.IssueId)) return issue.IssueId.Trim();
            return issue?.Title ?? string.Empty;
        }

        private static string FormatDate(DateTime? date)
        {
            if (!date.HasValue || date.Value == default(DateTime)) return string.Empty;
            return date.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        private static string BuildReferenceText(ViewpointModel viewpoint)
        {
            List<string> lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(viewpoint?.CoordinateMode))
            {
                lines.Add("Coordinate: " + viewpoint.CoordinateMode);
            }

            if (viewpoint?.ElementIds != null && viewpoint.ElementIds.Count > 0)
            {
                lines.Add("Element IDs: " + string.Join(", ", viewpoint.ElementIds));
            }

            if (viewpoint?.ComponentIfcGuids != null && viewpoint.ComponentIfcGuids.Count > 0)
            {
                lines.Add("IFC GUIDs: " + string.Join(", ", viewpoint.ComponentIfcGuids.Values));
            }

            if (viewpoint?.ClashModelFiles != null && viewpoint.ClashModelFiles.Count > 0)
            {
                lines.Add("Files: " + string.Join(" <-> ", viewpoint.ClashModelFiles));
            }

            if (viewpoint?.HasClashPoint == true)
            {
                lines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Clash Point: {0:G17}, {1:G17}, {2:G17}",
                    viewpoint.ClashPointX,
                    viewpoint.ClashPointY,
                    viewpoint.ClashPointZ));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string BuildImageText(ViewpointModel viewpoint)
        {
            List<string> lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(viewpoint?.SnapshotFilePath))
            {
                lines.Add("3D: " + viewpoint.SnapshotFilePath);
            }

            if (!string.IsNullOrWhiteSpace(viewpoint?.SnapshotFilePath2))
            {
                lines.Add("2D: " + viewpoint.SnapshotFilePath2);
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static void WriteColumns(XmlWriter writer)
        {
            int[] widths = { 6, 12, 14, 12, 34, 16, 24, 16, 14, 16, 14, 42, 32, 44, 44, 32, 44, 20, 32, 32, 32 };
            writer.WriteStartElement("cols");
            for (int i = 0; i < widths.Length; i++)
            {
                writer.WriteStartElement("col");
                writer.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("width", widths[i].ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("customWidth", "1");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteMetadataColumns(XmlWriter writer)
        {
            int[] widths = { 12, 18, 24, 28, 10 };
            writer.WriteStartElement("cols");
            for (int i = 0; i < widths.Length; i++)
            {
                writer.WriteStartElement("col");
                writer.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("width", widths[i].ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("customWidth", "1");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteRow(XmlWriter writer, int rowIndex, int totalColumns, string[] values, int styleIndex, double height)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowIndex.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("ht", height.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customHeight", "1");

            for (int i = 0; i < totalColumns; i++)
            {
                string value = i < values.Length ? values[i] : string.Empty;
                WriteInlineStringCell(writer, rowIndex, i + 1, value, styleIndex);
            }

            writer.WriteEndElement();
        }

        private static void WriteEmptyRow(XmlWriter writer, int rowIndex)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowIndex.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        private static void WriteInlineStringCell(XmlWriter writer, int rowIndex, int columnIndex, string value, int styleIndex)
        {
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", GetColumnName(columnIndex) + rowIndex.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is");
            writer.WriteStartElement("t");
            writer.WriteAttributeString("xml", "space", null, "preserve");
            writer.WriteString(value ?? string.Empty);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteMergeCell(XmlWriter writer, string range)
        {
            writer.WriteStartElement("mergeCell");
            writer.WriteAttributeString("ref", range);
            writer.WriteEndElement();
        }

        private static string GetColumnName(int columnNumber)
        {
            StringBuilder builder = new StringBuilder();
            while (columnNumber > 0)
            {
                int modulo = (columnNumber - 1) % 26;
                builder.Insert(0, (char)('A' + modulo));
                columnNumber = (columnNumber - modulo) / 26;
            }

            return builder.ToString();
        }

        private static string BuildContentTypes(List<ExcelImage> images)
        {
            return CreateXml(writer =>
            {
                writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                writer.WriteStartElement("Default");
                writer.WriteAttributeString("Extension", "rels");
                writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml");
                writer.WriteEndElement();
                writer.WriteStartElement("Default");
                writer.WriteAttributeString("Extension", "xml");
                writer.WriteAttributeString("ContentType", "application/xml");
                writer.WriteEndElement();

                bool hasPng = false;
                bool hasJpg = false;
                foreach (var img in images)
                {
                    if (img.Extension == "png") hasPng = true;
                    if (img.Extension == "jpg" || img.Extension == "jpeg") hasJpg = true;
                }
                if (hasPng)
                {
                    writer.WriteStartElement("Default");
                    writer.WriteAttributeString("Extension", "png");
                    writer.WriteAttributeString("ContentType", "image/png");
                    writer.WriteEndElement();
                }
                if (hasJpg)
                {
                    writer.WriteStartElement("Default");
                    writer.WriteAttributeString("Extension", "jpg");
                    writer.WriteAttributeString("ContentType", "image/jpeg");
                    writer.WriteEndElement();
                    writer.WriteStartElement("Default");
                    writer.WriteAttributeString("Extension", "jpeg");
                    writer.WriteAttributeString("ContentType", "image/jpeg");
                    writer.WriteEndElement();
                }

                WriteOverride(writer, "/docProps/app.xml", "application/vnd.openxmlformats-officedocument.extended-properties+xml");
                WriteOverride(writer, "/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml");
                WriteOverride(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
                WriteOverride(writer, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
                WriteOverride(writer, "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                WriteOverride(writer, "/xl/worksheets/sheet2.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");

                if (images.Count > 0)
                {
                    WriteOverride(writer, "/xl/drawings/drawing1.xml", "application/vnd.openxmlformats-officedocument.drawing+xml");
                }

                writer.WriteEndElement();
            });
        }

        private static void WriteOverride(XmlWriter writer, string partName, string contentType)
        {
            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", partName);
            writer.WriteAttributeString("ContentType", contentType);
            writer.WriteEndElement();
        }

        private static string BuildRootRelationships()
        {
            return CreateXml(writer =>
            {
                writer.WriteStartElement("Relationships", PackageRelationshipNamespace);
                WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
                WriteRelationship(writer, "rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml");
                WriteRelationship(writer, "rId3", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties", "docProps/app.xml");
                writer.WriteEndElement();
            });
        }

        private static string BuildWorkbookRelationships()
        {
            return CreateXml(writer =>
            {
                writer.WriteStartElement("Relationships", PackageRelationshipNamespace);
                WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet1.xml");
                WriteRelationship(writer, "rId2", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet2.xml");
                WriteRelationship(writer, "rId3", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
                writer.WriteEndElement();
            });
        }

        private static string BuildSheetRelationships()
        {
            return CreateXml(writer =>
            {
                writer.WriteStartElement("Relationships", PackageRelationshipNamespace);
                WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing", "../drawings/drawing1.xml");
                writer.WriteEndElement();
            });
        }

        private static string BuildDrawingXml(List<ExcelImage> images)
        {
            return CreateXml(writer =>
            {
                writer.WriteStartElement("xdr", "wsDr", "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing");
                writer.WriteAttributeString("xmlns", "a", null, "http://schemas.openxmlformats.org/drawingml/2006/main");
                writer.WriteAttributeString("xmlns", "r", null, RelationshipNamespace);

                for (int i = 0; i < images.Count; i++)
                {
                    var img = images[i];
                    writer.WriteStartElement("xdr", "twoCellAnchor", null);
                    writer.WriteAttributeString("editAs", "oneCell");

                    int startCol = img.IsSecond ? 14 : 13;
                    int startColOff = 50000;
                    int endCol = img.IsSecond ? 15 : 14;
                    int endColOff = 0;

                    if (img.TotalImagesInCell > 1)
                    {
                        long totalEmu = 2900000;
                        long gapEmu = 150000;
                        long stepEmu = totalEmu / img.TotalImagesInCell;
                        long widthEmu = stepEmu - gapEmu;
                        
                        startColOff = 50000 + (int)(img.ImageIndexInCell * stepEmu);
                        endCol = startCol; 
                        endColOff = startColOff + (int)widthEmu;
                    }

                    writer.WriteStartElement("xdr", "from", null);
                    writer.WriteElementString("xdr", "col", null, startCol.ToString(CultureInfo.InvariantCulture));
                    writer.WriteElementString("xdr", "colOff", null, startColOff.ToString(CultureInfo.InvariantCulture));
                    writer.WriteElementString("xdr", "row", null, img.RowIndex.ToString(CultureInfo.InvariantCulture));
                    writer.WriteElementString("xdr", "rowOff", null, "50000");
                    writer.WriteEndElement();

                    writer.WriteStartElement("xdr", "to", null);
                    writer.WriteElementString("xdr", "col", null, endCol.ToString(CultureInfo.InvariantCulture));
                    writer.WriteElementString("xdr", "colOff", null, endColOff.ToString(CultureInfo.InvariantCulture));
                    writer.WriteElementString("xdr", "row", null, (img.RowIndex + 1).ToString(CultureInfo.InvariantCulture));
                    writer.WriteElementString("xdr", "rowOff", null, "0");
                    writer.WriteEndElement();

                    writer.WriteStartElement("xdr", "pic", null);

                    writer.WriteStartElement("xdr", "nvPicPr", null);
                    writer.WriteStartElement("xdr", "cNvPr", null);
                    writer.WriteAttributeString("id", (i + 1).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("name", "Picture " + (i + 1));
                    writer.WriteEndElement();

                    writer.WriteStartElement("xdr", "cNvPicPr", null);
                    writer.WriteStartElement("a", "picLocks", null);
                    writer.WriteAttributeString("noChangeAspect", "1");
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndElement();

                    writer.WriteStartElement("xdr", "blipFill", null);
                    writer.WriteStartElement("a", "blip", null);
                    writer.WriteAttributeString("r", "embed", RelationshipNamespace, img.RelId);
                    writer.WriteEndElement();
                    writer.WriteStartElement("a", "stretch", null);
                    writer.WriteElementString("a", "fillRect", null, "");
                    writer.WriteEndElement();
                    writer.WriteEndElement();

                    writer.WriteStartElement("xdr", "spPr", null);
                    writer.WriteStartElement("a", "xfrm", null);
                    writer.WriteStartElement("a", "off", null);
                    writer.WriteAttributeString("x", "0");
                    writer.WriteAttributeString("y", "0");
                    writer.WriteEndElement();
                    writer.WriteStartElement("a", "ext", null);
                    writer.WriteAttributeString("cx", "0");
                    writer.WriteAttributeString("cy", "0");
                    writer.WriteEndElement();
                    writer.WriteEndElement();

                    writer.WriteStartElement("a", "prstGeom", null);
                    writer.WriteAttributeString("prst", "rect");
                    writer.WriteElementString("a", "avLst", null, "");
                    writer.WriteEndElement();

                    // Thêm hiệu ứng màu (Glow đỏ, đổ bóng, viền mờ) theo format
                    writer.WriteRaw("<a:effectLst><a:glow rad=\"63500\"><a:srgbClr val=\"FF0000\"><a:alpha val=\"40000\"/></a:srgbClr></a:glow><a:outerShdw blurRad=\"12700\" dist=\"12700\" dir=\"5400000\" rotWithShape=\"0\"><a:srgbClr val=\"000000\"><a:alpha val=\"100000\"/></a:srgbClr></a:outerShdw><a:softEdge rad=\"12700\"/></a:effectLst>");

                    writer.WriteEndElement();

                    writer.WriteEndElement();

                    writer.WriteElementString("xdr", "clientData", null, "");
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            });
        }

        private static string BuildDrawingRelationships(List<ExcelImage> images)
        {
            return CreateXml(writer =>
            {
                writer.WriteStartElement("Relationships", PackageRelationshipNamespace);
                foreach (var img in images)
                {
                    WriteRelationship(writer, img.RelId, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", "../media/" + Path.GetFileName(img.ZipPath));
                }
                writer.WriteEndElement();
            });
        }

        private static void WriteRelationship(XmlWriter writer, string id, string type, string target)
        {
            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", id);
            writer.WriteAttributeString("Type", type);
            writer.WriteAttributeString("Target", target);
            writer.WriteEndElement();
        }

        private static string BuildWorkbook()
        {
            return CreateXml(writer =>
            {
                writer.WriteStartElement("workbook", SpreadsheetNamespace);
                writer.WriteAttributeString("xmlns", "r", null, RelationshipNamespace);
                writer.WriteStartElement("sheets");
                WriteSheet(writer, MainSheetName, "1", "rId1");
                WriteSheet(writer, MetadataSheetName, "2", "rId2");
                writer.WriteEndElement();
                writer.WriteEndElement();
            });
        }

        private static void WriteSheet(XmlWriter writer, string name, string sheetId, string relationshipId)
        {
            writer.WriteStartElement("sheet");
            writer.WriteAttributeString("name", name);
            writer.WriteAttributeString("sheetId", sheetId);
            writer.WriteAttributeString("r", "id", RelationshipNamespace, relationshipId);
            writer.WriteEndElement();
        }

        private static string BuildCoreProperties()
        {
            return CreateXml(writer =>
            {
                writer.WriteStartElement("cp", "coreProperties", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
                writer.WriteAttributeString("xmlns", "dc", null, "http://purl.org/dc/elements/1.1/");
                writer.WriteAttributeString("xmlns", "dcterms", null, "http://purl.org/dc/terms/");
                writer.WriteAttributeString("xmlns", "dcmitype", null, "http://purl.org/dc/dcmitype/");
                writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteElementString("dc", "creator", null, Environment.UserName);
                writer.WriteElementString("cp", "lastModifiedBy", null, Environment.UserName);
                writer.WriteStartElement("dcterms", "created", null);
                writer.WriteAttributeString("xsi", "type", null, "dcterms:W3CDTF");
                writer.WriteString(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                writer.WriteEndElement();
                writer.WriteStartElement("dcterms", "modified", null);
                writer.WriteAttributeString("xsi", "type", null, "dcterms:W3CDTF");
                writer.WriteString(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                writer.WriteEndElement();
                writer.WriteEndElement();
            });
        }

        private static string BuildAppProperties()
        {
            return CreateXml(writer =>
            {
                writer.WriteStartElement("Properties", "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties");
                writer.WriteAttributeString("xmlns", "vt", null, "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes");
                writer.WriteElementString("Application", "Antigravity Issue Manager");
                writer.WriteElementString("DocSecurity", "0");
                writer.WriteElementString("ScaleCrop", "false");
                writer.WriteElementString("Company", "VILAI VIET");
                writer.WriteEndElement();
            });
        }

        private static string BuildStyles()
        {
            return CreateXml(writer =>
            {
                writer.WriteStartElement("styleSheet", SpreadsheetNamespace);
                writer.WriteRaw("<fonts count=\"6\"><font><sz val=\"10\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"20\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"13\"/><color rgb=\"FF122A49\"/><name val=\"Calibri\"/><family val=\"2\"/></font><font><sz val=\"13\"/><name val=\"Calibri\"/><family val=\"2\"/></font><font><sz val=\"13\"/><color rgb=\"FF475569\"/><name val=\"Calibri\"/><family val=\"2\"/></font><font><b/><sz val=\"13\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/><family val=\"2\"/></font></fonts>");
                writer.WriteRaw("<fills count=\"4\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF101F38\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF1F5F9\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>");
                writer.WriteRaw("<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style=\"thin\"><color rgb=\"FFCBD5E1\"/></left><right style=\"thin\"><color rgb=\"FFCBD5E1\"/></right><top style=\"thin\"><color rgb=\"FFCBD5E1\"/></top><bottom style=\"thin\"><color rgb=\"FFCBD5E1\"/></bottom><diagonal/></border></borders>");
                writer.WriteRaw("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
                writer.WriteRaw("<cellXfs count=\"11\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\"/></xf><xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/><xf numFmtId=\"0\" fontId=\"5\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf><xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf><xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\"/></xf><xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\"/></xf><xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/><xf numFmtId=\"0\" fontId=\"4\" fillId=\"3\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf><xf numFmtId=\"0\" fontId=\"5\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf></cellXfs>");
                writer.WriteRaw("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
                writer.WriteEndElement();
            });
        }

        private static void WriteTextEntry(ZipArchive archive, string path, string text)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path);
            using (Stream stream = entry.Open())
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(text);
            }
        }

        private static string CreateXml(Action<XmlWriter> write)
        {
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                OmitXmlDeclaration = true
            };

            StringBuilder builder = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(builder, settings))
            {
                write(writer);
            }

            return builder.ToString();
        }

        private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

        private class ExcelImage
        {
            public string FilePath { get; set; }
            public int RowIndex { get; set; }
            public string RelId { get; set; }
            public string ZipPath { get; set; }
            public string Extension { get; set; }
            public bool IsSecond { get; set; }
            public int ImageIndexInCell { get; set; }
            public int TotalImagesInCell { get; set; }
        }

        private class ExcelColumn
        {
            public ExcelColumn(string key, string label, bool editable)
            {
                Key = key;
                Label = label;
                Editable = editable;
            }

            public string Key { get; }
            public string Label { get; }
            public bool Editable { get; }
        }
    }
}
