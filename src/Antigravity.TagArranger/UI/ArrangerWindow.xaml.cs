using System;
using System.Globalization;
using System.Windows;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.TagArranger.Models;
using Antigravity.TagArranger.Services;

namespace Antigravity.TagArranger.UI
{
    public partial class ArrangerWindow : Window
    {
        private readonly UIDocument _uiDoc;
        private readonly ExternalEvent _externalEvent;
        private readonly ArrangerEventHandler _handler;

        public ArrangerWindow(UIDocument uiDoc, ExternalEvent externalEvent, ArrangerEventHandler handler)
        {
            InitializeComponent();
            _uiDoc = uiDoc;
            _externalEvent = externalEvent;
            _handler = handler;

            // Đăng ký callback để nhận kết quả từ EventHandler
            _handler.OnCompleted = OnActionCompleted;

            this.Loaded += ArrangerWindow_Loaded;
        }

        private void ArrangerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTagTypes();
        }

        // ── Align Buttons ─────────────────────────────────────────────────
        private void BtnAlignTop_Click(object sender, RoutedEventArgs e) => RaiseAction(ArrangeAction.AlignTop);
        private void BtnAlignBottom_Click(object sender, RoutedEventArgs e) => RaiseAction(ArrangeAction.AlignBottom);
        private void BtnAlignLeft_Click(object sender, RoutedEventArgs e) => RaiseAction(ArrangeAction.AlignLeft);
        private void BtnAlignRight_Click(object sender, RoutedEventArgs e) => RaiseAction(ArrangeAction.AlignRight);
        private void BtnAlignCenterH_Click(object sender, RoutedEventArgs e) => RaiseAction(ArrangeAction.AlignCenterH);
        private void BtnAlignCenterV_Click(object sender, RoutedEventArgs e) => RaiseAction(ArrangeAction.AlignCenterV);

        // ── Anti-Overlap Button ───────────────────────────────────────────
        private void BtnUntangle_Click(object sender, RoutedEventArgs e) => RaiseAction(ArrangeAction.AntiOverlap);

        // ── Distribute Buttons ────────────────────────────────────────────
        private void BtnDistributeH_Click(object sender, RoutedEventArgs e) => RaiseAction(ArrangeAction.DistributeH);
        private void BtnDistributeV_Click(object sender, RoutedEventArgs e) => RaiseAction(ArrangeAction.DistributeV);

        // ── Smart Stack Buttons ───────────────────────────────────────────
        private void BtnStackLeft_Click(object sender, RoutedEventArgs e)
        {
            _handler.HorizontalOffsetFeet = ParseMmToFeet(TxtHorizontalOffset.Text, 200.0);
            RaiseAction(ArrangeAction.SmartStackLeft);
        }

        private void BtnStackRight_Click(object sender, RoutedEventArgs e)
        {
            _handler.HorizontalOffsetFeet = ParseMmToFeet(TxtHorizontalOffset.Text, 200.0);
            RaiseAction(ArrangeAction.SmartStackRight);
        }

        // ── Leader Buttons ────────────────────────────────────────────────
        private void BtnStraightenLeaders_Click(object sender, RoutedEventArgs e)
        {
            _handler.LandingDistanceFeet = ParseMmToFeet(TxtLandingDistance.Text, 10.0);
            RaiseAction(ArrangeAction.StraightenLeaders);
        }

        private void BtnSetLeaderAngle_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtLeaderAngle.Text.Replace(",", "."),
                NumberStyles.Any, CultureInfo.InvariantCulture, out double angle) && angle > 0 && angle <= 90)
            {
                _handler.LeaderAngleDegrees = angle;
            }
            else
            {
                _handler.LeaderAngleDegrees = 45.0;
            }
            _handler.LandingDistanceFeet = ParseMmToFeet(TxtLandingDistance.Text, 10.0);
            RaiseAction(ArrangeAction.SetLeaderAngle);
        }

        private void BtnParallelizeLeaders_Click(object sender, RoutedEventArgs e)
        {
            RaiseAction(ArrangeAction.ParallelizeLeaders);
        }

        private void BtnOrthogonalLeaders_Click(object sender, RoutedEventArgs e)
        {
            RaiseAction(ArrangeAction.OrthogonalLeaders);
        }

        // ── Merge Tags ────────────────────────────────────────────────────
        private void BtnMergeTags_Click(object sender, RoutedEventArgs e)
        {
            if (CmbMergeScope.SelectedIndex == 1)
                _handler.MergeScope = MergeScope.ActiveView;
            else
                _handler.MergeScope = MergeScope.SelectedElements;

            if (RbSmartStack.IsChecked == true)
            {
                RaiseAction(ArrangeAction.MergeTagsSmartStack);
            }
            else if (RbMergePick.IsChecked == true)
            {
                // To allow PickPoint to work, we hide the window temporarily, 
                // the external event runs, and then the callback will show it again.
                this.Hide();
                RaiseAction(ArrangeAction.MergeTagsPickPoint);
            }
            else
            {
                RaiseAction(ArrangeAction.MergeTagsCenter);
            }
        }

        // ── Auto Tag ──────────────────────────────────────────────────────
        private void CmbAutoTagCategory_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                LoadTagTypes();
            }
        }

        private void LoadTagTypes()
        {
            if (_uiDoc == null || _uiDoc.Document == null) return;
            Document doc = _uiDoc.Document;
            BuiltInCategory tagCategory = BuiltInCategory.OST_WallTags;

            if (CmbAutoTagCategory.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                string catName = item.Content.ToString();
                switch (catName)
                {
                    case "Walls": tagCategory = BuiltInCategory.OST_WallTags; break;
                    case "Doors": tagCategory = BuiltInCategory.OST_DoorTags; break;
                    case "Windows": tagCategory = BuiltInCategory.OST_WindowTags; break;
                    case "Rooms": tagCategory = BuiltInCategory.OST_RoomTags; break;
                }
            }

            var symbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(tagCategory)
                .Cast<FamilySymbol>()
                .ToList();

            var list = new List<TagTypeItem>();
            list.Add(new TagTypeItem { Id = ElementId.InvalidElementId, Name = "<Default>" });

            foreach (var sym in symbols)
            {
                list.Add(new TagTypeItem { Id = sym.Id, Name = $"{sym.FamilyName} : {sym.Name}" });
            }

            CmbTagType.ItemsSource = list;
            CmbTagType.SelectedIndex = 0;
        }

        private void BtnAutoTag_Click(object sender, RoutedEventArgs e)
        {
            if (CmbAutoTagCategory.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                string catName = item.Content.ToString();
                switch (catName)
                {
                    case "Walls": _handler.AutoTagCategory = BuiltInCategory.OST_Walls; break;
                    case "Doors": _handler.AutoTagCategory = BuiltInCategory.OST_Doors; break;
                    case "Windows": _handler.AutoTagCategory = BuiltInCategory.OST_Windows; break;
                    case "Rooms": _handler.AutoTagCategory = BuiltInCategory.OST_Rooms; break;
                    default: _handler.AutoTagCategory = BuiltInCategory.OST_Walls; break;
                }
            }
            
            if (CmbTagType.SelectedItem is TagTypeItem tagType)
            {
                _handler.AutoTagTypeId = tagType.Id;
            }
            else
            {
                _handler.AutoTagTypeId = ElementId.InvalidElementId;
            }

            if (CmbAutoTagScope.SelectedIndex == 1)
                _handler.AutoTagScope = AutoTagScope.SelectedElements;
            else if (CmbAutoTagScope.SelectedIndex == 2)
                _handler.AutoTagScope = AutoTagScope.EntireProject;
            else
                _handler.AutoTagScope = AutoTagScope.ActiveView;

            _handler.AutoUntangle = ChkAutoUntangle.IsChecked == true;
            _handler.AutoTagHasLeader = ChkAutoTagLeader.IsChecked == true;
            _handler.AutoTagOffsetFeet = ParseMmToFeet(TxtAutoTagOffset.Text, 900.0);
            
            if (RbAutoTagPickPoint.IsChecked == true)
            {
                _handler.AutoTagPickPoint = true;
                this.Hide();
            }
            else
            {
                _handler.AutoTagPickPoint = false;
            }

            RaiseAction(ArrangeAction.AutoTag);
        }

        // ── Close Button ──────────────────────────────────────────────────
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ── Core Logic ────────────────────────────────────────────────────

        private void RaiseAction(ArrangeAction action)
        {
            _handler.CurrentAction = action;
            _handler.Options = BuildOptions();

            // Set AlignOffsetFeet if it's an Align action
            if (action == ArrangeAction.AlignTop || action == ArrangeAction.AlignBottom || 
                action == ArrangeAction.AlignLeft || action == ArrangeAction.AlignRight || 
                action == ArrangeAction.AlignCenterH || action == ArrangeAction.AlignCenterV)
            {
                _handler.AlignOffsetFeet = ParseMmToFeet(TxtAlignOffset.Text, 0.0);
            }

            TxtStatus.Text = $"Đang xử lý {action}...";

            var result = _externalEvent.Raise();
            if (result != ExternalEventRequest.Accepted)
            {
                TxtStatus.Text = "Revit đang bận. Vui lòng thử lại.";
            }
        }

        private ArrangeOptions BuildOptions()
        {
            double spacingMm = 5.0;
            if (double.TryParse(TxtSpacing.Text.Replace(",", "."),
                NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed) && parsed > 0)
            {
                spacingMm = parsed;
            }

            LeaderFormatStyle style = LeaderFormatStyle.Orthogonal;
            if (CmbAutoTagLeaderStyle != null)
            {
                if (CmbAutoTagLeaderStyle.SelectedIndex == 1) style = LeaderFormatStyle.Angled;
                else if (CmbAutoTagLeaderStyle.SelectedIndex == 2) style = LeaderFormatStyle.RevitDefault;
            }

            double angle = 45.0;
            if (TxtLeaderAngle != null && double.TryParse(TxtLeaderAngle.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedAngle))
            {
                angle = parsedAngle;
            }

            double maxMergeMeters = 0.0;
            if (TxtMaxMergeDistance != null && double.TryParse(TxtMaxMergeDistance.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedMergeDist))
            {
                maxMergeMeters = parsedMergeDist;
            }

            return new ArrangeOptions
            {
                MinSpacingFeet = spacingMm / 304.8, // mm → feet
                MaintainLeader = ChkMaintainLeader.IsChecked == true,
                IncludeTags = ChkIncludeTags.IsChecked == true,
                IncludeDimensions = ChkIncludeDimensions.IsChecked == true,
                AutoTagLeaderStyle = style,
                LeaderAngleDegrees = angle,
                MaxMergeDistanceFeet = maxMergeMeters * 3.28084 // meters → feet
            };
        }

        private static double ParseMmToFeet(string text, double defaultMm)
        {
            if (string.IsNullOrWhiteSpace(text)) return defaultMm / 304.8;
            
            // Handle negative values for offset
            if (double.TryParse(text.Replace(",", "."),
                NumberStyles.Any, CultureInfo.InvariantCulture, out double mm))
            {
                return mm / 304.8;
            }
            return defaultMm / 304.8;
        }

        private void OnActionCompleted(ArrangeResult result)
        {
            // Callback chạy trên Revit API thread → dispatch về UI thread
            Dispatcher.Invoke(() =>
            {
                // Re-show window if it was hidden (for PickPoint)
                if (this.Visibility != System.Windows.Visibility.Visible)
                {
                    this.Show();
                    this.Activate(); // Bring back to front
                }

                TxtStatus.Text = result.Message;
            });
        }
    }

    public class TagTypeItem
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
    }
}
