using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Autodesk.Revit.UI;
using Antigravity.Autojoin.Models;
using Antigravity.Autojoin.Services;

namespace Antigravity.Autojoin.UI
{
    public partial class MainWindow : Window
    {
        private readonly UIDocument _uidoc;
        private ObservableCollection<JoinRule> _rules;

        // ── ExternalEvent ──
        private readonly JoinEventHandler _handler;
        private readonly ExternalEvent    _exEvent;

        public MainWindow(UIDocument uidoc)
        {
            _uidoc = uidoc;
            InitializeComponent();

            _handler           = new JoinEventHandler();
            _handler.Completed += OnJoinCompleted;
            _exEvent           = ExternalEvent.Create(_handler);

            LoadRules();

            // Restore DMU toggle state from static property
            ChkDmuEnabled.IsChecked = AutoJoinUpdater.IsEnabled;
        }

        // ----------------------------------------------------------------
        //  LOAD / SAVE
        // ----------------------------------------------------------------

        private void LoadRules()
        {
            var list = JoinConfigService.Load();
            for (int i = 0; i < list.Count; i++)
                list[i].Order = i + 1;

            _rules = new ObservableCollection<JoinRule>(list);
            RulesListView.ItemsSource = _rules;
            SetStatus($"Loaded {_rules.Count} join rules.", isSuccess: true);
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e) => LoadRules();

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (HasDuplicateRule(out string duplicateMessage))
            {
                SetStatus(duplicateMessage, isSuccess: false);
                MessageBox.Show(duplicateMessage, "AutoJoin - Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ruleList = new List<JoinRule>(_rules);
            JoinConfigService.Save(ruleList);

            // Cập nhật rules cho DMU updater
            AutoJoinUpdater.RefreshRules(ruleList);

            SetStatus("✅  Configuration saved successfully.", isSuccess: true);
        }

        // ----------------------------------------------------------------
        //  ADD / REMOVE RULES
        // ----------------------------------------------------------------

        private void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            var existing = _rules.Select(r => (r.CategoryA, r.CategoryB)).ToHashSet();
            string newA = null, newB = null;

            var cats = JoinRule.SupportedCategories;
            foreach (var a in cats)
            {
                foreach (var b in cats)
                {
                    if (a == b) continue;
                    if (!existing.Contains((a, b)) && !existing.Contains((b, a)))
                    {
                        newA = a;
                        newB = b;
                        break;
                    }
                }
                if (newA != null) break;
            }

            if (newA == null) { newA = cats[0]; newB = cats[1]; }

            var rule = new JoinRule
            {
                Order        = _rules.Count + 1,
                CategoryA    = newA,
                CategoryB    = newB,
                IsEnabled    = true
            };

            _rules.Add(rule);
            RefreshOrders();
            SetStatus($"Added rule: {rule.DisplayName}", isSuccess: true);
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_rules == null || !IsLoaded) return;

            if (HasDuplicateRule(out string duplicateMessage))
                SetStatus(duplicateMessage, isSuccess: false);
        }

        private void BtnRemoveRule_Click(object sender, RoutedEventArgs e)
        {
            var selected = RulesListView.SelectedItem as JoinRule;
            if (selected == null)
            {
                SetStatus("Select a rule to remove.", isSuccess: false);
                return;
            }

            _rules.Remove(selected);
            RefreshOrders();
            SetStatus("Rule removed.", isSuccess: true);
        }

        private void RefreshOrders()
        {
            for (int i = 0; i < _rules.Count; i++)
                _rules[i].Order = i + 1;

            RulesListView.ItemsSource = null;
            RulesListView.ItemsSource = _rules;
        }

        // ----------------------------------------------------------------
        //  DMU TOGGLE
        // ----------------------------------------------------------------

        private void ChkDmu_Changed(object sender, RoutedEventArgs e)
        {
            bool enabled = ChkDmuEnabled.IsChecked == true;

            // Chỉ cần gán flag static, Updater đã đăng ký ở App.OnStartup sẽ tự check flag này
            AutoJoinUpdater.IsEnabled = enabled;

            // Lưu trạng thái vào config
            JoinConfigService.SaveDmuEnabled(enabled);

            SetStatus(enabled ? "⚡ Realtime Auto Join is ON." : "Realtime Auto Join is OFF.", isSuccess: enabled);

            // Nếu có thay đổi rules mà chưa lưu, cập nhật luôn cho updater
            AutoJoinUpdater.RefreshRules(new List<JoinRule>(_rules));
        }

        // ----------------------------------------------------------------
        //  JOIN / UNJOIN
        // ----------------------------------------------------------------

        private void BtnJoin_Click(object sender, RoutedEventArgs e)
        {
            if (HasDuplicateRule(out string duplicateMessage))
            {
                SetStatus(duplicateMessage, isSuccess: false);
                MessageBox.Show(duplicateMessage, "AutoJoin - Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _handler.Action = JoinAction.Join;
            _handler.Rules  = new List<JoinRule>(_rules);
            _handler.Scope  = RbSelection.IsChecked == true ? JoinScope.Selection : JoinScope.ActiveView;
            SetBusy(true);
            SetStatus("Joining geometry…");
            ClearResults();
            _exEvent.Raise();
        }

        private void BtnUnjoin_Click(object sender, RoutedEventArgs e)
        {
            if (HasDuplicateRule(out string duplicateMessage))
            {
                SetStatus(duplicateMessage, isSuccess: false);
                MessageBox.Show(duplicateMessage, "AutoJoin - Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                "Remove all geometry joins in the current scope?",
                "Confirm Unjoin",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            _handler.Action = JoinAction.Unjoin;
            _handler.Rules  = new List<JoinRule>(_rules);
            _handler.Scope  = RbSelection.IsChecked == true ? JoinScope.Selection : JoinScope.ActiveView;
            SetBusy(true);
            SetStatus("Unjoining geometry…");
            ClearResults();
            _exEvent.Raise();
        }

        private void OnJoinCompleted(JoinResult result, Exception error)
        {
            Dispatcher.Invoke(() =>
            {
                SetBusy(false);

                if (error != null)
                {
                    SetStatus($"❌  Error: {error.Message}", isSuccess: false);
                    MessageBox.Show(error.ToString(), "AutoJoin - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (result == null) return;

                // Update summary stats
                TxtTotalInt.Text = result.TotalIntersections.ToString();
                TxtJoined.Text   = result.JoinCount.ToString();
                TxtAlready.Text  = result.AlreadyCount.ToString();
                TxtFailed.Text   = result.SkipCount.ToString();

                // Update failed list
                FailedListView.ItemsSource = result.FailedJoins;

                SetStatus(JoinStatusFormatter.Format(_handler.Action, result), isSuccess: true);
            });
        }

        private void ClearResults()
        {
            TxtTotalInt.Text = "0";
            TxtJoined.Text   = "0";
            TxtAlready.Text  = "0";
            TxtFailed.Text   = "0";
            FailedListView.ItemsSource = null;
        }

        // ----------------------------------------------------------------
        //  HELPERS
        // ----------------------------------------------------------------

        private void SetBusy(bool busy)
        {
            // BtnJoin.IsEnabled = !busy;
            // BtnUnjoin.IsEnabled = !busy;
            PrgBar.Visibility   = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetStatus(string msg, bool isSuccess = false)
        {
            TxtStatus.Text       = msg;
            TxtStatus.Foreground = isSuccess
                ? new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xA0))
                : new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0xA8));
        }

        private bool HasDuplicateRule(out string message)
        {
            message = null;
            if (_rules == null) return false;

            var seen = new HashSet<string>();
            foreach (var rule in _rules)
            {
                if (string.IsNullOrEmpty(rule.CategoryA) || string.IsNullOrEmpty(rule.CategoryB))
                    continue;

                var pair = $"{rule.CategoryA}|{rule.CategoryB}";

                if (seen.Add(pair)) continue;

                message = $"Rule #{rule.Order}: the pair {JoinRule.FriendlyName(rule.CategoryA)} - {JoinRule.FriendlyName(rule.CategoryB)} already exists.";
                return true;
            }

            return false;
        }

        private static void ShowErrors(List<string> errors)
        {
            var msg = string.Join("\n", errors.GetRange(0, Math.Min(errors.Count, 10)));
            if (errors.Count > 10) msg += $"\n…and {errors.Count - 10} more errors.";
            MessageBox.Show(msg, "AutoJoin - Error Details", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public class CategoryNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return JoinRule.FriendlyName(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
