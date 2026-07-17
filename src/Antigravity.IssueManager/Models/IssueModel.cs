using System;

namespace Antigravity.IssueManager.Models
{
    public class IssueModel
    {
        public string IssueId { get; set; }
        public string IssueCode { get; set; }
        public string Title { get; set; }
        public string Level { get; set; }
        public string Folder { get; set; }
        public string AssignedTo { get; set; }
        public string DisplayTitle
        {
            get
            {
                if (string.IsNullOrWhiteSpace(IssueCode))
                {
                    return Title;
                }

                if (!string.IsNullOrWhiteSpace(Title) &&
                    Title.TrimStart().StartsWith(IssueCode, StringComparison.OrdinalIgnoreCase))
                {
                    return Title;
                }

                return $"{IssueCode} - {Title}";
            }
        }
        public string Description { get; set; }
        public string Status { get; set; }
        public string Author { get; set; }
        public DateTime CreationDate { get; set; }
        public string Distance { get; set; } // specific to Navisworks XML
        public ViewpointModel Viewpoint { get; set; }
        public string ClashFileSummary
        {
            get
            {
                if (Viewpoint?.ClashModelFiles == null || Viewpoint.ClashModelFiles.Count == 0)
                {
                    return "Files: (not found)";
                }

                string item1 = Viewpoint.ClashModelFiles.Count > 0 ? Viewpoint.ClashModelFiles[0] : "(not found)";
                string item2 = Viewpoint.ClashModelFiles.Count > 1 ? Viewpoint.ClashModelFiles[1] : "(not found)";
                return $"Files: {item1} <-> {item2}";
            }
        }
    }
}
