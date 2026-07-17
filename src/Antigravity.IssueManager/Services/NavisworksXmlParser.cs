using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Antigravity.IssueManager.Models;

namespace Antigravity.IssueManager.Services
{
    public class NavisworksXmlParser
    {
        private const string IfcGuidAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

        public string LastDiagnosticPath { get; private set; }

        public List<IssueModel> ParseReport(string xmlFilePath)
        {
            if (string.IsNullOrWhiteSpace(xmlFilePath)) throw new ArgumentException("XML file path is required.", nameof(xmlFilePath));

            List<IssueModel> issues = new List<IssueModel>();
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(xmlFilePath);

                // Navisworks XML can use a default or prefixed namespace. Match by LocalName.
                foreach (XmlElement node in GetDescendantsByLocalName(doc, "clashresult"))
                {
                    IssueModel issue = new IssueModel();
                    issue.IssueId = GetAttribute(node, "guid");
                    if (string.IsNullOrEmpty(issue.IssueId)) issue.IssueId = GetAttribute(node, "href");
                    if (string.IsNullOrEmpty(issue.IssueId)) issue.IssueId = Guid.NewGuid().ToString();

                    issue.Title = GetAttribute(node, "name");
                    if (string.IsNullOrEmpty(issue.Title)) issue.Title = "Unknown Clash";

                    issue.Status = GetAttribute(node, "status");
                    if (string.IsNullOrEmpty(issue.Status)) issue.Status = "New";

                    issue.Distance = GetAttribute(node, "distance");
                    if (string.IsNullOrEmpty(issue.Distance)) issue.Distance = "0";
                        
                    issue.Description = $"Navisworks Clash: {issue.Title}";
                    issue.Author = "Navisworks";
                    issue.CreationDate = DateTime.Now;

                    XmlElement parentTest = FindAncestorByLocalName(node, "clashtest");
                    issue.Folder = parentTest != null ? GetAttribute(parentTest, "name") : "Uncategorized";
                    if (string.IsNullOrWhiteSpace(issue.Folder)) issue.Folder = "Uncategorized";

                    // Parse clash point
                    issue.Viewpoint = new ViewpointModel();
                    ParseClashModelFiles(node, issue.Viewpoint);
                    ParseClashObjectData(node, issue.Viewpoint);
                    ParseSmartTagData(node, issue.Viewpoint);

                    XmlElement clashPointNode = GetFirstDescendantByLocalName(node, "clashpoint");
                    if (clashPointNode != null)
                    {
                        XmlElement pos3f = GetFirstDescendantByLocalName(clashPointNode, "pos3f");
                        if (pos3f != null)
                        {
                            double x = ParseDouble(GetAttribute(pos3f, "x"));
                            double y = ParseDouble(GetAttribute(pos3f, "y"));
                            double z = ParseDouble(GetAttribute(pos3f, "z"));
                                
                            // Convert mm (default metric Navisworks export) to meters
                            double toMeters = 0.001; 
                            issue.Viewpoint.ClashPointX = x * toMeters;
                            issue.Viewpoint.ClashPointY = y * toMeters;
                            issue.Viewpoint.ClashPointZ = z * toMeters;
                            issue.Viewpoint.HasClashPoint = true;
                        }
                    }

                    // Parse Images
                    string xmlDir = Path.GetDirectoryName(xmlFilePath);
                    List<XmlElement> imageNodes = new List<XmlElement>(GetDescendantsByLocalName(node, "image"));
                    if (imageNodes.Count > 0)
                    {
                        string img1Href = GetImageReference(imageNodes[0]);
                        if (!string.IsNullOrEmpty(img1Href))
                        {
                            issue.Viewpoint.SnapshotFilePath = ResolveImagePath(xmlDir, img1Href);
                                
                            // Navisworks often generates _image0.jpg and _image1.jpg
                            // If the XML only references the first, we can probe for the second.
                            if (imageNodes.Count > 1)
                            {
                                string img2Href = GetImageReference(imageNodes[1]);
                                if (!string.IsNullOrEmpty(img2Href))
                                {
                                    issue.Viewpoint.SnapshotFilePath2 = ResolveImagePath(xmlDir, img2Href);
                                }
                            }
                            else if (img1Href.IndexOf("_image0", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                string img2Href = img1Href.Replace("_image0", "_image1");
                                string potentialImg2 = ResolveImagePath(xmlDir, img2Href);
                                if (File.Exists(potentialImg2))
                                {
                                    issue.Viewpoint.SnapshotFilePath2 = potentialImg2;
                                }
                            }
                        }
                    }
                    else
                    {
                        string resultHref = GetAttribute(node, "href");
                        if (!string.IsNullOrWhiteSpace(resultHref))
                        {
                            issue.Viewpoint.SnapshotFilePath = ResolveImagePath(xmlDir, resultHref);
                        }
                    }

                    issues.Add(issue);
                }

                LastDiagnosticPath = WriteDiagnostics(xmlFilePath, issues, doc);
            }
            catch (Exception ex)
            {
                // In a real scenario, use Antigravity.Core.Logger
                System.Diagnostics.Debug.WriteLine("Error parsing XML: " + ex.Message);
                LastDiagnosticPath = WriteErrorDiagnostic(xmlFilePath, ex);
            }
            return issues;
        }

        private static IEnumerable<XmlElement> GetDescendantsByLocalName(XmlNode node, string localName)
        {
            if (node == null) yield break;

            foreach (XmlNode child in node.ChildNodes)
            {
                XmlElement element = child as XmlElement;
                if (element == null) continue;

                if (string.Equals(element.LocalName, localName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return element;
                }

                foreach (XmlElement descendant in GetDescendantsByLocalName(element, localName))
                {
                    yield return descendant;
                }
            }
        }

        private static XmlElement FindAncestorByLocalName(XmlNode node, string localName)
        {
            XmlNode parent = node.ParentNode;
            while (parent != null)
            {
                if (parent is XmlElement element && string.Equals(element.LocalName, localName, StringComparison.OrdinalIgnoreCase))
                {
                    return element;
                }
                parent = parent.ParentNode;
            }
            return null;
        }

        private static XmlElement GetFirstDescendantByLocalName(XmlNode node, string localName)
        {
            foreach (XmlElement element in GetDescendantsByLocalName(node, localName))
            {
                return element;
            }

            return null;
        }

        private static XmlElement GetFirstChildByLocalName(XmlNode node, string localName)
        {
            if (node == null) return null;

            foreach (XmlNode child in node.ChildNodes)
            {
                XmlElement element = child as XmlElement;
                if (element != null &&
                    string.Equals(element.LocalName, localName, StringComparison.OrdinalIgnoreCase))
                {
                    return element;
                }
            }

            return null;
        }

        private static string GetAttribute(XmlElement element, string name)
        {
            if (element == null) return string.Empty;

            foreach (XmlAttribute attribute in element.Attributes)
            {
                if (string.Equals(attribute.LocalName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return attribute.Value;
                }
            }

            return string.Empty;
        }

        private static double ParseDouble(string value)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }

            double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
            return result;
        }

        private static void ParseClashModelFiles(XmlElement clashNode, ViewpointModel viewpoint)
        {
            foreach (XmlElement clashObject in GetDescendantsByLocalName(clashNode, "clashobject"))
            {
                AddModelFileName(viewpoint.ClashModelFiles, FindModelFileName(clashObject));
                if (viewpoint.ClashModelFiles.Count >= 2) return;
            }

            if (viewpoint.ClashModelFiles.Count == 0)
            {
                foreach (XmlElement element in GetDescendantsByLocalName(clashNode, "smarttag"))
                {
                    XmlElement nameNode = GetFirstChildByLocalName(element, "name");
                    XmlElement valueNode = GetFirstChildByLocalName(element, "value");
                    string name = nameNode?.InnerText?.ToLowerInvariant() ?? string.Empty;
                    string value = valueNode?.InnerText ?? string.Empty;

                    if (name.Contains("file") || name.Contains("model") || LooksLikeModelFile(value))
                    {
                        AddModelFileName(viewpoint.ClashModelFiles, value);
                        if (viewpoint.ClashModelFiles.Count >= 2) return;
                    }
                }
            }
        }

        private static void ParseClashObjectData(XmlElement clashNode, ViewpointModel viewpoint)
        {
            foreach (XmlElement clashObject in GetDescendantsByLocalName(clashNode, "clashobject"))
            {
                List<string> objectIds = new List<string>();
                List<string> objectIfcGuids = new List<string>();

                foreach (XmlElement attribute in GetDescendantsByLocalName(clashObject, "objectattribute"))
                {
                    ParseNameValueNode(attribute, viewpoint, objectIds, objectIfcGuids);
                }

                foreach (XmlElement smartTag in GetDescendantsByLocalName(clashObject, "smarttag"))
                {
                    ParseNameValueNode(smartTag, viewpoint, objectIds, objectIfcGuids);
                }

                if (objectIfcGuids.Count > 0)
                {
                    foreach (string id in objectIds)
                    {
                        if (!viewpoint.ComponentIfcGuids.ContainsKey(id))
                        {
                            viewpoint.ComponentIfcGuids[id] = objectIfcGuids[0];
                        }
                    }
                }
            }
        }

        private static void ParseSmartTagData(XmlElement clashNode, ViewpointModel viewpoint)
        {
            foreach (XmlElement smartTag in GetDescendantsByLocalName(clashNode, "smarttag"))
            {
                ParseNameValueNode(smartTag, viewpoint, null, null);
            }
        }

        private static void ParseNameValueNode(
            XmlElement node,
            ViewpointModel viewpoint,
            List<string> objectIds,
            List<string> objectIfcGuids)
        {
            XmlElement nameNode = GetFirstChildByLocalName(node, "name");
            XmlElement valueNode = GetFirstChildByLocalName(node, "value");
            string name = nameNode?.InnerText?.Trim() ?? string.Empty;
            string value = valueNode?.InnerText?.Trim() ?? string.Empty;

            if (IsElementIdLabel(name))
            {
                foreach (string id in ExtractIdentifiers(value))
                {
                    AddUnique(viewpoint.ElementIds, id);
                    AddUnique(objectIds, id);
                }
            }

            if (IsIfcGuidLabel(name) || IsLikelyIfcGuid(value))
            {
                foreach (string ifcGuid in ExtractIfcGuids(value))
                {
                    AddUnique(viewpoint.ElementIds, ifcGuid);
                    AddUnique(objectIfcGuids, ifcGuid);
                }
            }

            if (IsItemNameLabel(name))
            {
                AddModelFileName(viewpoint.ClashModelFiles, value);
            }
        }

        private static bool IsElementIdLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            string normalized = name.Trim().ToLowerInvariant();
            return normalized.Contains("element id") ||
                   normalized.Contains("item id") ||
                   normalized.Contains("authoring tool id") ||
                   normalized == "elementid" ||
                   normalized == "itemid" ||
                   normalized == "id";
        }

        private static bool IsIfcGuidLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            string normalized = name.Trim().ToLowerInvariant().Replace(" ", string.Empty);
            return normalized.Contains("ifcguid") ||
                   normalized.Contains("globalid");
        }

        private static bool IsItemNameLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            string normalized = name.Trim().ToLowerInvariant();
            return normalized.Contains("item name") ||
                   normalized.Contains("model name") ||
                   normalized.Contains("file name");
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value)) return;

            string trimmed = value.Trim();
            foreach (string existing in values)
            {
                if (string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            values.Add(trimmed);
        }

        private static IEnumerable<string> ExtractIdentifiers(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) yield break;

            bool yielded = false;
            foreach (Match match in Regex.Matches(value, @"(?<![\w$])(?:\d{1,18}|[0-9A-Za-z_$]{22})(?![\w$])"))
            {
                string candidate = match.Value.Trim();
                if (string.IsNullOrWhiteSpace(candidate)) continue;

                yielded = true;
                yield return candidate;
            }

            if (!yielded)
            {
                string trimmed = value.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    yield return trimmed;
                }
            }
        }

        private static IEnumerable<string> ExtractIfcGuids(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) yield break;

            foreach (Match match in Regex.Matches(value, @"(?<![\w$])[0-9A-Za-z_$]{22}(?![\w$])"))
            {
                string candidate = match.Value.Trim();
                if (IsLikelyIfcGuid(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static bool IsLikelyIfcGuid(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            string trimmed = value.Trim();
            if (trimmed.Length != 22) return false;

            foreach (char c in trimmed)
            {
                if (IfcGuidAlphabet.IndexOf(c) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static string FindModelFileName(XmlElement clashObject)
        {
            string best = GetModelFileCandidate(clashObject);
            if (!string.IsNullOrWhiteSpace(best)) return best;

            best = ExtractModelFileName(clashObject.OuterXml);
            if (!string.IsNullOrWhiteSpace(best)) return best;

            string fallback = string.Empty;
            foreach (XmlElement element in GetAllDescendantElements(clashObject))
            {
                best = GetModelFileCandidate(element);
                if (!string.IsNullOrWhiteSpace(best)) return best;

                if (string.IsNullOrWhiteSpace(fallback))
                {
                    fallback = GetObjectNameCandidate(element);
                }
            }

            return fallback;
        }

        private static IEnumerable<XmlElement> GetAllDescendantElements(XmlNode node)
        {
            if (node == null) yield break;

            foreach (XmlNode child in node.ChildNodes)
            {
                XmlElement element = child as XmlElement;
                if (element == null) continue;

                yield return element;

                foreach (XmlElement descendant in GetAllDescendantElements(element))
                {
                    yield return descendant;
                }
            }
        }

        private static string GetModelFileCandidate(XmlElement element)
        {
            foreach (XmlAttribute attribute in element.Attributes)
            {
                string fileName = ExtractModelFileName(attribute.Value);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    return fileName;
                }

                if (string.Equals(attribute.LocalName, "filename", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(attribute.LocalName, "file", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(attribute.LocalName, "model", StringComparison.OrdinalIgnoreCase))
                {
                    return attribute.Value;
                }
            }

            string directTextFile = ExtractModelFileName(GetDirectText(element));
            if (!string.IsNullOrWhiteSpace(directTextFile))
            {
                return directTextFile;
            }

            if (string.Equals(element.LocalName, "filename", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(element.LocalName, "file", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(element.LocalName, "model", StringComparison.OrdinalIgnoreCase))
            {
                return element.InnerText;
            }

            return string.Empty;
        }

        private static string GetObjectNameCandidate(XmlElement element)
        {
            foreach (XmlAttribute attribute in element.Attributes)
            {
                if (string.Equals(attribute.LocalName, "name", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(attribute.LocalName, "displayname", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(attribute.LocalName, "filename", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(attribute.LocalName, "file", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(attribute.LocalName, "model", StringComparison.OrdinalIgnoreCase))
                {
                    string value = CleanObjectName(attribute.Value);
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }

            if (string.Equals(element.LocalName, "displayname", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(element.LocalName, "filename", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(element.LocalName, "file", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(element.LocalName, "model", StringComparison.OrdinalIgnoreCase))
            {
                return CleanObjectName(element.InnerText);
            }

            return string.Empty;
        }

        private static void AddModelFileName(List<string> files, string value)
        {
            string fileName = NormalizeModelFileName(value);
            if (string.IsNullOrWhiteSpace(fileName)) return;

            foreach (string existing in files)
            {
                if (string.Equals(existing, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            files.Add(fileName);
        }

        private static string NormalizeModelFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            string normalized = Uri.UnescapeDataString(value.Trim().Trim('"'));
            if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri) && uri.IsFile)
            {
                normalized = uri.LocalPath;
            }

            normalized = normalized.Replace('/', Path.DirectorySeparatorChar);
            string fileName = Path.GetFileName(normalized);
            return string.IsNullOrWhiteSpace(fileName) ? normalized : fileName;
        }

        private static string CleanObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            string normalized = Uri.UnescapeDataString(value.Trim().Trim('"'));
            normalized = normalized.Replace('/', Path.DirectorySeparatorChar);
            string fileName = Path.GetFileName(normalized);
            if (!string.IsNullOrWhiteSpace(fileName)) normalized = fileName;

            if (normalized.Equals("LcOaNode", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("LcOaSceneBaseUserName", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("LcOaSceneBaseClassName", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Item Name", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Element ID", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Item ID", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("ID", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return normalized;
        }

        private static string GetDirectText(XmlElement element)
        {
            if (element == null) return string.Empty;

            foreach (XmlNode child in element.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Text || child.NodeType == XmlNodeType.CDATA)
                {
                    string text = child.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }

            return string.Empty;
        }

        private static string ExtractModelFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            string normalized = Uri.UnescapeDataString(value.Trim().Trim('"'));
            Match match = Regex.Match(
                normalized,
                @"[^\r\n\t<>|?*""]+\.(?:nwc|nwd|rvt|ifc|dwg)",
                RegexOptions.IgnoreCase);

            if (!match.Success) return string.Empty;

            return NormalizeModelFileName(match.Value);
        }

        private static bool LooksLikeModelFile(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            string lower = value.ToLowerInvariant();
            return lower.Contains(".nwc") ||
                   lower.Contains(".nwd") ||
                   lower.Contains(".rvt") ||
                   lower.Contains(".ifc") ||
                   lower.Contains(".dwg");
        }

        private static string GetImageReference(XmlElement imageNode)
        {
            string value = GetAttribute(imageNode, "href");
            if (!string.IsNullOrWhiteSpace(value)) return value;

            value = GetAttribute(imageNode, "src");
            if (!string.IsNullOrWhiteSpace(value)) return value;

            value = GetAttribute(imageNode, "file");
            if (!string.IsNullOrWhiteSpace(value)) return value;

            value = GetAttribute(imageNode, "filename");
            if (!string.IsNullOrWhiteSpace(value)) return value;

            value = GetAttribute(imageNode, "path");
            if (!string.IsNullOrWhiteSpace(value)) return value;

            return imageNode.InnerText?.Trim() ?? string.Empty;
        }

        private static string ResolveImagePath(string xmlDir, string imageReference)
        {
            if (string.IsNullOrWhiteSpace(imageReference)) return string.Empty;

            string normalized = Uri.UnescapeDataString(imageReference.Trim().Trim('"'));

            if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri) && uri.IsFile)
            {
                return uri.LocalPath;
            }

            normalized = normalized.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized))
            {
                return normalized;
            }

            string combined = Path.GetFullPath(Path.Combine(xmlDir ?? string.Empty, normalized));
            if (File.Exists(combined))
            {
                return combined;
            }

            string fileName = Path.GetFileName(normalized);
            if (!string.IsNullOrWhiteSpace(fileName) && Directory.Exists(xmlDir))
            {
                try
                {
                    foreach (string candidate in Directory.GetFiles(xmlDir, fileName, SearchOption.AllDirectories))
                    {
                        return candidate;
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return combined;
        }

        private static string WriteDiagnostics(string xmlFilePath, List<IssueModel> issues, XmlDocument doc)
        {
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AntigravityIssueManager");
                Directory.CreateDirectory(logDir);

                string logPath = Path.Combine(logDir, "navisworks_xml_last_load.txt");
                using (StreamWriter writer = new StreamWriter(logPath, false))
                {
                    writer.WriteLine("Navisworks XML load diagnostic");
                    writer.WriteLine("XML: " + xmlFilePath);
                    writer.WriteLine("Issues: " + (issues?.Count ?? 0));
                    writer.WriteLine();

                    int index = 0;
                    foreach (XmlElement clash in GetDescendantsByLocalName(doc, "clashresult"))
                    {
                        index++;
                        writer.WriteLine("=== Clash " + index + " ===");
                        writer.WriteLine("name=" + GetAttribute(clash, "name"));
                        writer.WriteLine("guid=" + GetAttribute(clash, "guid"));

                        int objectIndex = 0;
                        foreach (XmlElement clashObject in GetDescendantsByLocalName(clash, "clashobject"))
                        {
                            objectIndex++;
                            writer.WriteLine("-- clashobject " + objectIndex + " --");
                            writer.WriteLine("modelFile=" + FindModelFileName(clashObject));
                            WriteInterestingNodes(writer, clashObject);
                        }

                        if (index >= 5) break;
                        writer.WriteLine();
                    }
                }

                return logPath;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string WriteErrorDiagnostic(string xmlFilePath, Exception exception)
        {
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AntigravityIssueManager");
                Directory.CreateDirectory(logDir);

                string logPath = Path.Combine(logDir, "navisworks_xml_last_load.txt");
                using (StreamWriter writer = new StreamWriter(logPath, false))
                {
                    writer.WriteLine("Navisworks XML load diagnostic");
                    writer.WriteLine("XML: " + xmlFilePath);
                    writer.WriteLine("Parse failed.");
                    writer.WriteLine(exception);
                }

                return logPath;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void WriteInterestingNodes(StreamWriter writer, XmlElement root)
        {
            int count = 0;
            foreach (XmlElement element in GetAllDescendantElements(root))
            {
                string name = element.LocalName ?? string.Empty;
                string text = GetDirectText(element);
                string attributes = FormatAttributes(element);

                if (LooksInteresting(name, text, attributes))
                {
                    writer.WriteLine(name + " | attrs=[" + attributes + "] | text=[" + text + "]");
                    count++;
                }

                if (count >= 80)
                {
                    writer.WriteLine("... truncated ...");
                    return;
                }
            }
        }

        private static string FormatAttributes(XmlElement element)
        {
            List<string> parts = new List<string>();
            foreach (XmlAttribute attribute in element.Attributes)
            {
                parts.Add(attribute.LocalName + "=" + attribute.Value);
            }

            return string.Join("; ", parts);
        }

        private static bool LooksInteresting(string name, string text, string attributes)
        {
            string haystack = (name + " " + text + " " + attributes).ToLowerInvariant();
            return haystack.Contains("nwc") ||
                   haystack.Contains("nwd") ||
                   haystack.Contains("rvt") ||
                   haystack.Contains("ifc") ||
                   haystack.Contains("dwg") ||
                   haystack.Contains("item") ||
                   haystack.Contains("element") ||
                   haystack.Contains("file") ||
                   haystack.Contains("model") ||
                   haystack.Contains("name") ||
                   haystack.Contains("id");
        }
    }
}
