using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Antigravity.IssueManager.Models;

namespace Antigravity.IssueManager.UI
{
    public partial class ExportIssueSelectionDialog : Window
    {
        private readonly ObservableCollection<ExportIssueItem> _items;

        public ExportIssueSelectionDialog(IEnumerable<IssueModel> issues, IssueModel preferredIssue)
        {
            InitializeComponent();

            List<IssueModel> issueList = issues?.ToList() ?? new List<IssueModel>();
            bool selectOnlyPreferred = preferredIssue != null && issueList.Contains(preferredIssue);
            _items = new ObservableCollection<ExportIssueItem>(
                issueList.Select(issue => new ExportIssueItem(
                    issue,
                    selectOnlyPreferred ? ReferenceEquals(issue, preferredIssue) : true)));

            foreach (ExportIssueItem item in _items)
            {
                item.PropertyChanged += ExportIssueItem_PropertyChanged;
            }

            LvExportIssues.ItemsSource = _items;
            UpdateSelectionCount();
        }

        public List<IssueModel> SelectedIssues
        {
            get
            {
                return _items
                    .Where(item => item.IsSelected)
                    .Select(item => item.Issue)
                    .ToList();
            }
        }

        private void ExportIssueItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExportIssueItem.IsSelected))
            {
                UpdateSelectionCount();
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            SetAll(true);
        }

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            SetAll(false);
        }

        private void SetAll(bool selected)
        {
            foreach (ExportIssueItem item in _items)
            {
                item.IsSelected = selected;
            }

            UpdateSelectionCount();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedIssues.Count == 0)
            {
                MessageBox.Show("Please select at least one issue to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void UpdateSelectionCount()
        {
            int selected = _items.Count(item => item.IsSelected);
            TxtSelectionCount.Text = $"Selected: {selected}/{_items.Count}";
        }

        private class ExportIssueItem : INotifyPropertyChanged
        {
            private bool _isSelected;

            public ExportIssueItem(IssueModel issue, bool isSelected)
            {
                Issue = issue;
                _isSelected = isSelected;
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public IssueModel Issue { get; }

            public bool IsSelected
            {
                get { return _isSelected; }
                set
                {
                    if (_isSelected == value)
                    {
                        return;
                    }

                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public string Title
            {
                get { return Issue?.DisplayTitle ?? Issue?.Title ?? "(Untitled)"; }
            }

            public string Detail
            {
                get
                {
                    string level = string.IsNullOrWhiteSpace(Issue?.Level) ? "" : "Level: " + Issue.Level + " | ";
                    return level + "Status: " + (Issue?.Status ?? "") + " | Folder: " + (Issue?.Folder ?? "");
                }
            }
        }
    }
}
