using System.Collections.Generic;

namespace Antigravity.IssueManager.Models
{
    public class IssueGroupModel
    {
        public string GroupName { get; set; }
        public List<IssueModel> Issues { get; set; } = new List<IssueModel>();

        public string DisplayTitle
        {
            get { return $"{GroupName} ({Issues.Count})"; }
        }
    }
}
