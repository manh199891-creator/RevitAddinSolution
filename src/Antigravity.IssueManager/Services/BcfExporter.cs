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
    public static class BcfExporter
    {
        private const string IfcGuidAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

        public static void ExportToBcf(
            List<IssueModel> issues,
            string outputFilePath,
            IEnumerable<string> fallbackModelFileNames = null)
        {
            if (issues == null) throw new ArgumentNullException(nameof(issues));
            if (string.IsNullOrWhiteSpace(outputFilePath)) throw new ArgumentException("Output file path is required.", nameof(outputFilePath));

            string outputDir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            if (File.Exists(outputFilePath))
            {
                File.Delete(outputFilePath);
            }

            using (FileStream fileStream = new FileStream(outputFilePath, FileMode.CreateNew))
            using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                WriteBcfVersion(archive);
                WriteProject(archive);

                foreach (IssueModel issue in issues)
                {
                    string issueId = NormalizeGuid(issue.IssueId);
                    string issueFolder = issueId + "/";
                    string viewpointGuid = Guid.NewGuid().ToString();

                    WriteMarkup(archive, issueFolder, issue, issueId, viewpointGuid, fallbackModelFileNames);
                    WriteViewpoint(archive, issueFolder, issue.Viewpoint, viewpointGuid);
                    WriteSnapshot(archive, issueFolder, issue.Viewpoint);
                }
            }
        }

        private static void WriteBcfVersion(ZipArchive archive)
        {
            ZipArchiveEntry entry = archive.CreateEntry("bcf.version");
            using (Stream stream = entry.Open())
            using (XmlWriter writer = CreateXmlWriter(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Version");
                writer.WriteAttributeString("VersionId", "2.1");
                writer.WriteAttributeString("xsi", "noNamespaceSchemaLocation", "http://www.w3.org/2001/XMLSchema-instance", "version.xsd");
                writer.WriteElementString("DetailedVersion", "2.1");
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteProject(ZipArchive archive)
        {
            ZipArchiveEntry entry = archive.CreateEntry("project.bcfp");
            using (Stream stream = entry.Open())
            using (XmlWriter writer = CreateXmlWriter(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("ProjectExtension");
                writer.WriteStartElement("Project");
                writer.WriteAttributeString("ProjectId", Guid.NewGuid().ToString());
                writer.WriteElementString("Name", "Exported BCF");
                writer.WriteEndElement();
                writer.WriteElementString("ExtensionSchema", "extensions.xsd");
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteMarkup(
            ZipArchive archive,
            string issueFolder,
            IssueModel issue,
            string issueId,
            string viewpointGuid,
            IEnumerable<string> fallbackModelFileNames)
        {
            ZipArchiveEntry entry = archive.CreateEntry(issueFolder + "markup.bcf");
            using (Stream stream = entry.Open())
            using (XmlWriter writer = CreateXmlWriter(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Markup");
                writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");

                WriteHeader(writer, issue?.Viewpoint, fallbackModelFileNames);

                writer.WriteStartElement("Topic");
                writer.WriteAttributeString("Guid", issueId);
                writer.WriteAttributeString("TopicType", "Issue");
                writer.WriteAttributeString("TopicStatus", string.IsNullOrWhiteSpace(issue.Status) ? "Active" : issue.Status);
                writer.WriteElementString("Title", issue.DisplayTitle ?? issue.Title ?? "Untitled");
                writer.WriteElementString("CreationDate", GetIsoDate(issue.CreationDate));
                writer.WriteElementString("CreationAuthor", issue.Author ?? Environment.UserName);
                if (!string.IsNullOrWhiteSpace(issue.Description))
                {
                    writer.WriteElementString("Description", issue.Description);
                }
                writer.WriteEndElement(); // End Topic

                if (viewpointGuid != null)
                {
                    writer.WriteStartElement("Comment");
                    writer.WriteAttributeString("Guid", Guid.NewGuid().ToString().ToUpperInvariant());
                    writer.WriteElementString("Date", GetIsoDate(issue.CreationDate));
                    writer.WriteElementString("Author", issue.Author ?? Environment.UserName);
                    writer.WriteElementString("Comment", BuildCommentText(issue));
                    writer.WriteStartElement("Viewpoint");
                    writer.WriteAttributeString("Guid", viewpointGuid);
                    writer.WriteEndElement();
                    writer.WriteEndElement(); // End Comment

                    writer.WriteStartElement("Viewpoints");
                    writer.WriteAttributeString("Guid", viewpointGuid);
                    writer.WriteElementString("Viewpoint", "viewpoint.bcfv");
                    writer.WriteElementString("Snapshot", "snapshot.png");
                    writer.WriteEndElement(); // End Viewpoints
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static string BuildCommentText(IssueModel issue)
        {
            List<string> lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(issue?.DisplayTitle ?? issue?.Title))
            {
                lines.Add(issue.DisplayTitle ?? issue.Title);
            }

            if (!string.IsNullOrWhiteSpace(issue?.Description))
            {
                lines.Add(issue.Description);
            }

            if (!string.IsNullOrWhiteSpace(issue?.Distance))
            {
                lines.Add("Distance: " + issue.Distance);
            }

            ViewpointModel viewpoint = issue?.Viewpoint;
            if (!string.IsNullOrWhiteSpace(viewpoint?.CoordinateMode))
            {
                lines.Add("Coordinate Mode: " + viewpoint.CoordinateMode);
            }

            if (viewpoint?.HasClashPoint == true)
            {
                lines.Add(
                    "Clash Point: " +
                    ToInvariantString(viewpoint.ClashPointX) + " " +
                    ToInvariantString(viewpoint.ClashPointY) + " " +
                    ToInvariantString(viewpoint.ClashPointZ));
            }

            if (viewpoint?.ElementIds != null && viewpoint.ElementIds.Count > 0)
            {
                lines.Add("Element IDs: " + string.Join(", ", viewpoint.ElementIds));
            }

            return lines.Count == 0 ? "Issue exported from Antigravity Issue Manager" : string.Join(Environment.NewLine, lines);
        }

        private static void WriteHeader(
            XmlWriter writer,
            ViewpointModel viewpoint,
            IEnumerable<string> fallbackModelFileNames)
        {
            writer.WriteStartElement("Header");

            foreach (string fileName in GetRelatedModelFileNames(viewpoint, fallbackModelFileNames))
            {
                writer.WriteStartElement("File");
                writer.WriteElementString("Filename", fileName);
                writer.WriteElementString("Date", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        private static IEnumerable<string> GetRelatedModelFileNames(
            ViewpointModel viewpoint,
            IEnumerable<string> fallbackModelFileNames)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in EnumerateModelFileNameCandidates(viewpoint, fallbackModelFileNames))
            {
                string fileName = NormalizeRelatedModelFileName(value);
                if (!string.IsNullOrWhiteSpace(fileName) && seen.Add(fileName))
                {
                    yield return fileName;
                }
            }
        }

        private static IEnumerable<string> EnumerateModelFileNameCandidates(
            ViewpointModel viewpoint,
            IEnumerable<string> fallbackModelFileNames)
        {
            if (viewpoint?.ClashModelFiles != null)
            {
                foreach (string value in viewpoint.ClashModelFiles)
                {
                    yield return value;
                }
            }

            if (fallbackModelFileNames != null)
            {
                foreach (string value in fallbackModelFileNames)
                {
                    yield return value;
                }
            }
        }

        private static string NormalizeRelatedModelFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string trimmed = value.Trim();
            try
            {
                string fileName = Path.GetFileName(trimmed.Replace('/', Path.DirectorySeparatorChar));
                return IsSupportedTrimbleReferenceModel(fileName) ? fileName : null;
            }
            catch
            {
                return IsSupportedTrimbleReferenceModel(trimmed) ? trimmed : null;
            }
        }

        private static bool IsSupportedTrimbleReferenceModel(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string extension = Path.GetExtension(fileName.Trim());
            return extension.Equals(".nwc", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".nwd", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".ifc", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".dwg", StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteViewpoint(ZipArchive archive, string issueFolder, ViewpointModel viewpoint, string viewpointGuid)
        {
            ZipArchiveEntry entry = archive.CreateEntry(issueFolder + "viewpoint.bcfv");
            using (Stream stream = entry.Open())
            using (XmlWriter writer = CreateXmlWriter(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("VisualizationInfo");
                writer.WriteAttributeString("Guid", viewpointGuid);

                bool hasClipCenter = TryGetClippingCenter(
                    viewpoint?.ClippingPlanes,
                    out double clipCenterX,
                    out double clipCenterY,
                    out double clipCenterZ,
                    out double clipRadius);
                double cameraX = viewpoint?.CameraX ?? 0;
                double cameraY = viewpoint?.CameraY ?? 0;
                double cameraZ = viewpoint?.CameraZ ?? 0;
                StabilizeCameraEyeTowardClipCenter(
                    hasClipCenter,
                    clipCenterX,
                    clipCenterY,
                    clipCenterZ,
                    clipRadius,
                    viewpoint?.CameraDirectionX ?? 0,
                    viewpoint?.CameraDirectionY ?? 0,
                    viewpoint?.CameraDirectionZ ?? 0,
                    ref cameraX,
                    ref cameraY,
                    ref cameraZ);

                bool isOrthogonal = viewpoint?.IsOrthogonal == true;
                writer.WriteStartElement(isOrthogonal ? "OrthogonalCamera" : "PerspectiveCamera");
                WriteXyz(writer, "CameraViewPoint", cameraX, cameraY, cameraZ);
                WriteXyz(writer, "CameraDirection", viewpoint?.CameraDirectionX ?? 0, viewpoint?.CameraDirectionY ?? 0, viewpoint?.CameraDirectionZ ?? 0);
                WriteXyz(writer, "CameraUpVector", viewpoint?.CameraUpX ?? 0, viewpoint?.CameraUpY ?? 0, viewpoint?.CameraUpZ ?? 0);
                if (isOrthogonal)
                {
                    double scale = viewpoint?.ViewToWorldScale > 0 ? viewpoint.ViewToWorldScale : 10.0;
                    writer.WriteElementString("ViewToWorldScale", ToInvariantString(scale));
                }
                else
                {
                    writer.WriteElementString("FieldOfView", "60");
                }
                writer.WriteEndElement();

                if (viewpoint?.ClippingPlanes != null && viewpoint.ClippingPlanes.Count > 0)
                {
                    writer.WriteStartElement("ClippingPlanes");
                    foreach (ClippingPlaneModel plane in viewpoint.ClippingPlanes)
                    {
                        if (!IsValidClippingPlane(plane))
                        {
                            continue;
                        }

                        writer.WriteStartElement("ClippingPlane");
                        WriteXyz(writer, "Location", plane.LocationX, plane.LocationY, plane.LocationZ);
                        GetOutwardClippingDirection(plane, hasClipCenter, clipCenterX, clipCenterY, clipCenterZ, out double dx, out double dy, out double dz);
                        WriteXyz(writer, "Direction", dx, dy, dz);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static bool IsValidClippingPlane(ClippingPlaneModel plane)
        {
            return IsFinite(plane.LocationX) &&
                   IsFinite(plane.LocationY) &&
                   IsFinite(plane.LocationZ) &&
                   NormalizeXyz(plane.DirectionX, plane.DirectionY, plane.DirectionZ, out _, out _, out _);
        }

        private static bool TryGetClippingCenter(
            List<ClippingPlaneModel> planes,
            out double centerX,
            out double centerY,
            out double centerZ,
            out double radius)
        {
            centerX = centerY = centerZ = 0;
            radius = 0;
            if (planes == null || planes.Count == 0)
            {
                return false;
            }

            int count = 0;
            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double minZ = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;
            double maxZ = double.NegativeInfinity;
            foreach (ClippingPlaneModel plane in planes)
            {
                if (!IsValidClippingPlane(plane))
                {
                    continue;
                }

                centerX += plane.LocationX;
                centerY += plane.LocationY;
                centerZ += plane.LocationZ;
                minX = Math.Min(minX, plane.LocationX);
                minY = Math.Min(minY, plane.LocationY);
                minZ = Math.Min(minZ, plane.LocationZ);
                maxX = Math.Max(maxX, plane.LocationX);
                maxY = Math.Max(maxY, plane.LocationY);
                maxZ = Math.Max(maxZ, plane.LocationZ);
                count++;
            }

            if (count == 0)
            {
                centerX = centerY = centerZ = 0;
                radius = 0;
                return false;
            }

            centerX /= count;
            centerY /= count;
            centerZ /= count;
            double width = maxX - minX;
            double depth = maxY - minY;
            double height = maxZ - minZ;
            radius = Math.Sqrt(width * width + depth * depth + height * height) * 0.5;
            return true;
        }

        private static void GetOutwardClippingDirection(
            ClippingPlaneModel plane,
            bool hasClipCenter,
            double centerX,
            double centerY,
            double centerZ,
            out double dx,
            out double dy,
            out double dz)
        {
            NormalizeXyz(plane.DirectionX, plane.DirectionY, plane.DirectionZ, out dx, out dy, out dz);
            if (!hasClipCenter)
            {
                return;
            }

            double fromCenterX = plane.LocationX - centerX;
            double fromCenterY = plane.LocationY - centerY;
            double fromCenterZ = plane.LocationZ - centerZ;
            double dot = dx * fromCenterX + dy * fromCenterY + dz * fromCenterZ;
            if (dot < 0)
            {
                dx = -dx;
                dy = -dy;
                dz = -dz;
            }
        }

        private static void StabilizeCameraEyeTowardClipCenter(
            bool hasClipCenter,
            double centerX,
            double centerY,
            double centerZ,
            double clipRadius,
            double directionX,
            double directionY,
            double directionZ,
            ref double cameraX,
            ref double cameraY,
            ref double cameraZ)
        {
            if (!hasClipCenter ||
                !IsFinite(cameraX) ||
                !IsFinite(cameraY) ||
                !IsFinite(cameraZ) ||
                !NormalizeXyz(directionX, directionY, directionZ, out double dx, out double dy, out double dz))
            {
                return;
            }

            double toCenterX = centerX - cameraX;
            double toCenterY = centerY - cameraY;
            double toCenterZ = centerZ - cameraZ;
            double dot = toCenterX * dx + toCenterY * dy + toCenterZ * dz;
            if (dot >= 0)
            {
                double distance = Math.Sqrt(toCenterX * toCenterX + toCenterY * toCenterY + toCenterZ * toCenterZ);
                double maxStableDistance = Math.Max(clipRadius * 12.0, 200.0);
                if (IsFinite(distance) && distance <= maxStableDistance)
                {
                    return;
                }
            }

            double stableDistance = Math.Max(clipRadius * 3.0, 15.0);
            cameraX = centerX - dx * stableDistance;
            cameraY = centerY - dy * stableDistance;
            cameraZ = centerZ - dz * stableDistance;
        }

        private static bool NormalizeXyz(double x, double y, double z, out double nx, out double ny, out double nz)
        {
            double length = Math.Sqrt(x * x + y * y + z * z);
            if (!IsFinite(length) || length <= 1e-9)
            {
                nx = ny = nz = 0;
                return false;
            }

            nx = x / length;
            ny = y / length;
            nz = z / length;
            return true;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void WriteSnapshot(ZipArchive archive, string issueFolder, ViewpointModel viewpoint)
        {
            string snapshotPath = viewpoint?.SnapshotFilePath;
            if (string.IsNullOrWhiteSpace(snapshotPath) || !File.Exists(snapshotPath)) return;

            ZipArchiveEntry entry = archive.CreateEntry(issueFolder + "snapshot.png");
            using (Stream entryStream = entry.Open())
            using (FileStream sourceStream = new FileStream(snapshotPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                sourceStream.CopyTo(entryStream);
            }
        }

        private static XmlWriter CreateXmlWriter(Stream stream)
        {
            return XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                OmitXmlDeclaration = false
            });
        }

        private static void WriteXyz(XmlWriter writer, string elementName, double x, double y, double z)
        {
            writer.WriteStartElement(elementName);
            writer.WriteElementString("X", ToInvariantString(x));
            writer.WriteElementString("Y", ToInvariantString(y));
            writer.WriteElementString("Z", ToInvariantString(z));
            writer.WriteEndElement();
        }

        private static string ToInvariantString(double value)
        {
            return value.ToString("G17", CultureInfo.InvariantCulture);
        }

        private static string GetIsoDate(DateTime value)
        {
            DateTime date = value == default(DateTime) ? DateTime.Now : value;
            return date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }

        private static string NormalizeGuid(string value)
        {
            if (Guid.TryParse(value, out Guid guid))
            {
                return guid.ToString().ToUpperInvariant();
            }

            return Guid.NewGuid().ToString().ToUpperInvariant();
        }

        internal static string ToIfcGuid(Guid guid)
        {
            byte[] bytes = guid.ToByteArray();
            uint data1 = BitConverter.ToUInt32(bytes, 0);
            uint data2 = BitConverter.ToUInt16(bytes, 4);
            uint data3 = BitConverter.ToUInt16(bytes, 6);

            uint[] parts =
            {
                data1 / 16777216,
                data1 % 16777216,
                data2 * 256 + data3 / 256,
                (data3 % 256) * 65536 + (uint)bytes[8] * 256 + bytes[9],
                (uint)bytes[10] * 65536 + (uint)bytes[11] * 256 + bytes[12],
                (uint)bytes[13] * 65536 + (uint)bytes[14] * 256 + bytes[15]
            };

            StringBuilder builder = new StringBuilder(22);
            builder.Append(ToIfcBase64(parts[0], 2));
            for (int i = 1; i < parts.Length; i++)
            {
                builder.Append(ToIfcBase64(parts[i], 4));
            }

            return builder.ToString();
        }

        private static string ToIfcBase64(uint value, int length)
        {
            char[] chars = new char[length];
            for (int i = length - 1; i >= 0; i--)
            {
                chars[i] = IfcGuidAlphabet[(int)(value % 64)];
                value /= 64;
            }

            return new string(chars);
        }

        private static bool IsIfcGuid(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length != 22)
            {
                return false;
            }

            foreach (char c in value.Trim())
            {
                if (IfcGuidAlphabet.IndexOf(c) < 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
