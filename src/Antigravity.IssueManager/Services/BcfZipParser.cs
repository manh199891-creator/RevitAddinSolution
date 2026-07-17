using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Antigravity.IssueManager.Models;

namespace Antigravity.IssueManager.Services
{
    public class BcfZipParser
    {
        public List<IssueModel> ParseBcfZip(string zipFilePath)
        {
            List<IssueModel> issues = new List<IssueModel>();
            string tempDir = Path.Combine(Path.GetTempPath(), "AntigravityBcf", Guid.NewGuid().ToString());
            string logFile = Path.Combine(Path.GetTempPath(), "AntigravityIssueManager_AutoLoadLog.txt");

            try
            {
                File.AppendAllText(logFile, $"[{DateTime.Now}] ParseBcfZip started. Extracting to {tempDir}\n");
                Directory.CreateDirectory(tempDir);
                ZipFile.ExtractToDirectory(zipFilePath, tempDir);
                File.AppendAllText(logFile, $"[{DateTime.Now}] ZipFile extracted.\n");

                // In BCF, each issue is in its own subfolder (named with a GUID)
                string[] issueDirs = Directory.GetDirectories(tempDir);
                if (File.Exists(Path.Combine(tempDir, "markup.bcf")))
                {
                    issueDirs = new[] { tempDir };
                }

                foreach (string dir in issueDirs)
                {
                    IssueModel issue = new IssueModel();
                    issue.Viewpoint = new ViewpointModel();
                    string markupPath = Path.Combine(dir, "markup.bcf");
                    string viewpointPath = null;
                    string snapshotPath = null;

                    if (File.Exists(markupPath))
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.Load(markupPath);

                        foreach (XmlNode fileNode in SelectLocalNodes(doc, "File"))
                        {
                            string fileName = SelectSingleLocal(fileNode, "Filename")?.InnerText
                                ?? GetAttribute(fileNode, "Filename")
                                ?? GetAttribute(fileNode, "filename");
                            AddElementId(issue.Viewpoint.ClashModelFiles, fileName);
                        }

                        XmlNode topicNode = SelectSingleLocal(doc, "Topic");
                        if (topicNode != null)
                        {
                            issue.IssueId = GetAttribute(topicNode, "Guid") ?? Path.GetFileName(dir);
                            string rawTitle = SelectSingleLocal(topicNode, "Title")?.InnerText ?? "Untitled";
                            issue.Description = SelectSingleLocal(topicNode, "Description")?.InnerText ?? "";
                            ApplyTrimbleIssueCode(issue, rawTitle);
                            issue.Status = GetAttribute(topicNode, "TopicStatus")
                                ?? SelectSingleLocal(topicNode, "TopicStatus")?.InnerText
                                ?? "Open";
                            issue.Author = SelectSingleLocal(topicNode, "CreationAuthor")?.InnerText ?? "Unknown";
                        }

                        foreach (XmlNode commentNode in SelectLocalNodes(doc, "Comment"))
                        {
                            string commentText = commentNode.InnerText;
                            string coordinateMode = TryExtractCoordinateMode(commentText);
                            if (!string.IsNullOrWhiteSpace(coordinateMode))
                            {
                                issue.Viewpoint.CoordinateMode = coordinateMode;
                                break;
                            }
                        }

                        XmlNode viewpointReferenceNode = SelectSingleLocal(doc, "Viewpoints")
                            ?? SelectSingleLocal(doc, "ViewPoint");
                        if (viewpointReferenceNode != null)
                        {
                            string viewpointFile = SelectSingleLocal(viewpointReferenceNode, "Viewpoint")?.InnerText;
                            string snapshotFile = SelectSingleLocal(viewpointReferenceNode, "Snapshot")?.InnerText;
                            if (!string.IsNullOrWhiteSpace(viewpointFile))
                            {
                                viewpointPath = Path.Combine(dir, viewpointFile.Trim());
                            }

                            if (!string.IsNullOrWhiteSpace(snapshotFile))
                            {
                                snapshotPath = Path.Combine(dir, snapshotFile.Trim());
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(viewpointPath) || !File.Exists(viewpointPath))
                    {
                        string[] viewpointFiles = Directory.GetFiles(dir, "*.bcfv");
                        if (viewpointFiles.Length > 0)
                        {
                            viewpointPath = viewpointFiles[0];
                        }
                    }

                    if (string.IsNullOrEmpty(snapshotPath) || !File.Exists(snapshotPath))
                    {
                        string pngSnapshot = Path.Combine(dir, "snapshot.png");
                        string jpgSnapshot = Path.Combine(dir, "snapshot.jpg");
                        if (File.Exists(pngSnapshot))
                        {
                            snapshotPath = pngSnapshot;
                        }
                        else if (File.Exists(jpgSnapshot))
                        {
                            snapshotPath = jpgSnapshot;
                        }
                    }

                    if (File.Exists(snapshotPath))
                    {
                        issue.Viewpoint.SnapshotFilePath = snapshotPath; // We keep it in temp folder
                    }

                    if (File.Exists(viewpointPath))
                    {
                        XmlDocument viewDoc = new XmlDocument();
                        viewDoc.Load(viewpointPath);

                        XmlNode orthogonalCamera = SelectSingleLocal(viewDoc, "OrthogonalCamera");
                        issue.Viewpoint.IsOrthogonal = orthogonalCamera != null;
                        if (orthogonalCamera != null)
                        {
                            TryParseDouble(SelectSingleLocal(orthogonalCamera, "ViewToWorldScale")?.InnerText, out double viewToWorldScale);
                            issue.Viewpoint.ViewToWorldScale = viewToWorldScale;
                        }

                        // Parse camera location
                        XmlNode cameraLocation = SelectSingleLocal(viewDoc, "CameraViewPoint");
                        
                        if (cameraLocation != null)
                        {
                            TryParseDouble(SelectSingleLocal(cameraLocation, "X")?.InnerText, out double x);
                            TryParseDouble(SelectSingleLocal(cameraLocation, "Y")?.InnerText, out double y);
                            TryParseDouble(SelectSingleLocal(cameraLocation, "Z")?.InnerText, out double z);
                            issue.Viewpoint.CameraX = x;
                            issue.Viewpoint.CameraY = y;
                            issue.Viewpoint.CameraZ = z;
                        }

                        // Parse camera direction
                        XmlNode cameraDir = SelectSingleLocal(viewDoc, "CameraDirection");
                        if (cameraDir != null)
                        {
                            TryParseDouble(SelectSingleLocal(cameraDir, "X")?.InnerText, out double dx);
                            TryParseDouble(SelectSingleLocal(cameraDir, "Y")?.InnerText, out double dy);
                            TryParseDouble(SelectSingleLocal(cameraDir, "Z")?.InnerText, out double dz);
                            issue.Viewpoint.CameraDirectionX = dx;
                            issue.Viewpoint.CameraDirectionY = dy;
                            issue.Viewpoint.CameraDirectionZ = dz;
                        }

                        // Parse camera up vector
                        XmlNode cameraUp = SelectSingleLocal(viewDoc, "CameraUpVector");
                        if (cameraUp != null)
                        {
                            TryParseDouble(SelectSingleLocal(cameraUp, "X")?.InnerText, out double ux);
                            TryParseDouble(SelectSingleLocal(cameraUp, "Y")?.InnerText, out double uy);
                            TryParseDouble(SelectSingleLocal(cameraUp, "Z")?.InnerText, out double uz);
                            issue.Viewpoint.CameraUpX = ux;
                            issue.Viewpoint.CameraUpY = uy;
                            issue.Viewpoint.CameraUpZ = uz;
                        }

                        XmlNodeList clippingPlanes = viewDoc.SelectNodes("//*[local-name()='ClippingPlane']");
                        if (clippingPlanes != null)
                        {
                            foreach (XmlNode clippingPlane in clippingPlanes)
                            {
                                XmlNode location = SelectSingleLocal(clippingPlane, "Location");
                                XmlNode direction = SelectSingleLocal(clippingPlane, "Direction");
                                if (location == null || direction == null)
                                {
                                    continue;
                                }

                                TryParseDouble(SelectSingleLocal(location, "X")?.InnerText, out double lx);
                                TryParseDouble(SelectSingleLocal(location, "Y")?.InnerText, out double ly);
                                TryParseDouble(SelectSingleLocal(location, "Z")?.InnerText, out double lz);
                                TryParseDouble(SelectSingleLocal(direction, "X")?.InnerText, out double dx);
                                TryParseDouble(SelectSingleLocal(direction, "Y")?.InnerText, out double dy);
                                TryParseDouble(SelectSingleLocal(direction, "Z")?.InnerText, out double dz);

                                issue.Viewpoint.ClippingPlanes.Add(new ClippingPlaneModel
                                {
                                    LocationX = lx,
                                    LocationY = ly,
                                    LocationZ = lz,
                                    DirectionX = dx,
                                    DirectionY = dy,
                                    DirectionZ = dz
                                });
                            }
                        }

                        // Parse selected elements
                        XmlNodeList selectedElements = viewDoc.SelectNodes("//*[local-name()='Component']");
                        if (selectedElements != null)
                        {
                            foreach (XmlNode comp in selectedElements)
                            {
                                string ifcGuid = GetAttribute(comp, "IfcGuid");
                                
                                // In BCF schema, AuthoringToolId is often an attribute of Component, sometimes a child node.
                                string authoringId = GetAttribute(comp, "AuthoringToolId");
                                if (string.IsNullOrEmpty(authoringId))
                                {
                                    authoringId = SelectSingleLocal(comp, "AuthoringToolId")?.InnerText;
                                }

                                AddElementId(issue.Viewpoint.ElementIds, authoringId);
                                if (!string.IsNullOrWhiteSpace(authoringId) && !string.IsNullOrWhiteSpace(ifcGuid))
                                {
                                    issue.Viewpoint.ComponentIfcGuids[authoringId.Trim()] = ifcGuid.Trim();
                                }

                                if (string.IsNullOrWhiteSpace(authoringId))
                                {
                                    AddElementId(issue.Viewpoint.ElementIds, ifcGuid);
                                }
                            }
                        }
                    }

                    issues.Add(issue);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error parsing BCF: " + ex.Message);
                try { File.AppendAllText(logFile, $"[{DateTime.Now}] Error in ParseBcfZip: {ex.ToString()}\n"); } catch {}
            }
            // Note: In production, we should NOT delete the temp folder immediately if the UI still needs to load the images.
            // Temp folders should be cleaned up when the app closes or the tool is re-run.

            return issues;
        }

        private static XmlNode SelectSingleLocal(XmlNode node, string localName)
        {
            return node.SelectSingleNode($".//*[local-name()='{localName}']");
        }

        private static XmlNodeList SelectLocalNodes(XmlNode node, string localName)
        {
            return node.SelectNodes($".//*[local-name()='{localName}']");
        }

        private static string GetAttribute(XmlNode node, string localName)
        {
            if (node?.Attributes == null) return null;

            foreach (XmlAttribute attribute in node.Attributes)
            {
                if (string.Equals(attribute.LocalName, localName, StringComparison.OrdinalIgnoreCase))
                {
                    return attribute.Value;
                }
            }

            return null;
        }

        private static void ApplyTrimbleIssueCode(IssueModel issue, string rawTitle)
        {
            string title = NormalizeText(rawTitle);
            string description = NormalizeText(issue.Description);

            if (TryExtractIssueCodeFromTitle(title, out string code, out string titleWithoutCode))
            {
                issue.IssueCode = code;
                issue.Title = string.IsNullOrWhiteSpace(titleWithoutCode) ? title : titleWithoutCode;
                return;
            }

            if (TryExtractIssueCode(description, out code))
            {
                issue.IssueCode = code;
            }

            issue.Title = string.IsNullOrWhiteSpace(title) ? "Untitled" : title;
        }

        private static bool TryExtractIssueCodeFromTitle(string title, out string code, out string titleWithoutCode)
        {
            code = null;
            titleWithoutCode = title;
            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            string[] lines = title
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            if (lines.Length == 0)
            {
                return false;
            }

            Match firstLineMatch = Regex.Match(
                lines[0],
                @"^(?<code>[A-Z]{2,}[A-Z0-9]*-\d+)\s*[:\-–—]?\s*(?<rest>.*)$",
                RegexOptions.IgnoreCase);
            if (!firstLineMatch.Success)
            {
                return false;
            }

            code = firstLineMatch.Groups["code"].Value.ToUpperInvariant();
            string rest = firstLineMatch.Groups["rest"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(rest))
            {
                titleWithoutCode = rest;
            }
            else if (lines.Length > 1)
            {
                titleWithoutCode = string.Join(" ", lines.Skip(1));
            }
            else
            {
                titleWithoutCode = title;
            }

            return true;
        }

        private static bool TryExtractIssueCode(string value, out string code)
        {
            code = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            Match match = Regex.Match(value, @"\b(?<code>[A-Z]{2,}[A-Z0-9]*-\d+)\b", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            code = match.Groups["code"].Value.ToUpperInvariant();
            return true;
        }

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(value.Trim(), @"\s+", " ");
        }

        private static string TryExtractCoordinateMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            Match match = Regex.Match(
                value,
                @"Coordinate\s+Mode\s*:\s*(?<mode>Internal|Shared)",
                RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return string.Empty;
            }

            string mode = match.Groups["mode"].Value;
            return mode.Equals("Shared", StringComparison.OrdinalIgnoreCase) ? "Shared" : "Internal";
        }

        private static void AddElementId(List<string> elementIds, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            string trimmed = value.Trim();
            if (!elementIds.Contains(trimmed))
            {
                elementIds.Add(trimmed);
            }
        }

        private static bool TryParseDouble(string value, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
                || double.TryParse(value, out result);
        }
    }
}
