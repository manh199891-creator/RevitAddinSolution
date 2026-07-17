using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DoorClearanceBox.Core;

namespace DoorClearanceBox.UI
{
    /// <summary>
    /// Code-behind for ClearanceBoxWindow.xaml.
    /// Handles scope selection, door counting, and returns user settings to the command.
    /// </summary>
    public partial class ClearanceBoxWindow : Window
    {
        // ── Settings returned to the command ────────────────────────────────────
        public bool UseActiveView           { get; private set; } = true;
        public bool IncludeDoors            { get; private set; } = true;
        public bool IncludeWindows          { get; private set; } = true;
        public bool IncludeCurtainPanels    { get; private set; } = true;
        public bool IncludeCurtainWalls     { get; private set; } = true;
        public bool TagIfc                  { get; private set; } = true;
        public bool SkipExisting             { get; private set; } = true;
        public bool UndrawMode               { get; private set; } = false;
        public bool Confirmed               { get; private set; } = false;
        public Constants.GeometryType SelectedGeometry { get; private set; } = Constants.GeometryType.Rectangle;

        public List<Level> SelectedLevels       { get; private set; } = new List<Level>();
        public List<Level> SelectedLinkLevels   { get; private set; } = new List<Level>();
        public RevitLinkInstance SelectedLink  { get; private set; }
        public bool UseLevel                   { get; private set; } = false;
        public bool UseLink                    { get; private set; } = false;
        public bool UseEntireModel             { get; private set; } = false;

        public HashSet<ElementId> SelectedDoorTypeIds { get; private set; } = new HashSet<ElementId>();
        public HashSet<ElementId> SelectedWindowTypeIds { get; private set; } = new HashSet<ElementId>();
        public HashSet<ElementId> SelectedCurtainPanelTypeIds { get; private set; } = new HashSet<ElementId>();
        public HashSet<ElementId> SelectedCurtainWallTypeIds { get; private set; } = new HashSet<ElementId>();

        private bool _isLoaded = false;

        // ── Dependencies ────────────────────────────────────────────────────────
        private readonly Document _doc;
        private readonly View     _activeView;
        private IList<Element>    _currentElements;

        // ── Constructor ─────────────────────────────────────────────────────────
        public ClearanceBoxWindow(Document doc, View activeView)
        {
            InitializeComponent();
            _doc        = doc;
            _activeView = activeView;
        }

        // ── Lifecycle ───────────────────────────────────────────────────────────

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                WindowHeader.SubtitleText = $"v{Constants.Version}  \u00B7  Revit 2022-2025";
                
                // Populate Levels
                var levels = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .OfType<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();
                 LstLevels.ItemsSource = levels;

                // Populate Links
                var links = new FilteredElementCollector(_doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .OfType<RevitLinkInstance>()
                    .Where(l => l.GetLinkDocument() != null)
                    .OrderBy(l => l.Name)
                    .ToList();
                CboLinks.ItemsSource = links;
                CboLinks.DisplayMemberPath = "Name";
                if (links.Count > 0) CboLinks.SelectedIndex = 0;

                // Populate Door Types
                var doorTypes = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsElementType()
                    .OfType<FamilySymbol>()
                    .OrderBy(t => t.FamilyName).ThenBy(t => t.Name)
                    .Select(t => new TypeViewModel { Id = t.Id, Name = $"{t.FamilyName} - {t.Name}" })
                    .ToList();
                if (LstDoorTypes != null)
                {
                    LstDoorTypes.ItemsSource = doorTypes;
                    LstDoorTypes.SelectAll();
                }

                // Populate Window Types
                var windowTypes = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Windows)
                    .WhereElementIsElementType()
                    .OfType<FamilySymbol>()
                    .OrderBy(t => t.FamilyName).ThenBy(t => t.Name)
                    .Select(t => new TypeViewModel { Id = t.Id, Name = $"{t.FamilyName} - {t.Name}" })
                    .ToList();
                if (LstWindowTypes != null)
                {
                    LstWindowTypes.ItemsSource = windowTypes;
                    LstWindowTypes.SelectAll();
                }

                // Populate Curtain Panel Types
                var panelTypes = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_CurtainWallPanels)
                    .WhereElementIsElementType()
                    .OfType<ElementType>()
                    .OrderBy(t => t.FamilyName).ThenBy(t => t.Name)
                    .Select(t => new TypeViewModel { Id = t.Id, Name = $"{t.FamilyName} - {t.Name}" })
                    .ToList();
                if (LstCurtainPanelTypes != null)
                {
                    LstCurtainPanelTypes.ItemsSource = panelTypes;
                    LstCurtainPanelTypes.SelectAll();
                }

                // Populate Curtain Wall Types
                var wallTypes = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Walls)
                    .WhereElementIsElementType()
                    .OfType<WallType>()
                    .Where(t => t.Kind == WallKind.Curtain)
                    .OrderBy(t => t.FamilyName).ThenBy(t => t.Name)
                    .Select(t => new TypeViewModel { Id = t.Id, Name = $"{t.FamilyName} - {t.Name}" })
                    .ToList();
                if (LstCurtainWallTypes != null)
                {
                    LstCurtainWallTypes.ItemsSource = wallTypes;
                    LstCurtainWallTypes.SelectAll();
                }

                Category_Changed(null, null);
                
                _isLoaded = true;
                RefreshDoorCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading window: {ex.Message}\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Scope change ────────────────────────────────────────────────────────

        private void Scope_Changed(object sender, RoutedEventArgs e)
        {
            RefreshDoorCount();
        }

        private void Category_Changed(object sender, RoutedEventArgs e)
        {
            if (LstDoorTypes != null)
                LstDoorTypes.Visibility = (ChkDoors?.IsChecked == true) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            if (LstWindowTypes != null)
                LstWindowTypes.Visibility = (ChkWindows?.IsChecked == true) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            if (LstCurtainPanelTypes != null)
                LstCurtainPanelTypes.Visibility = (ChkCurtainPanels?.IsChecked == true) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            if (LstCurtainWallTypes != null)
                LstCurtainWallTypes.Visibility = (ChkCurtainWalls?.IsChecked == true) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            RefreshDoorCount();
        }

        private void TypeFilter_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RefreshDoorCount();
        }

        private void RefreshDoorCount()
        {
            if (!_isLoaded) return;
            
            try
            {
                if (_doc == null) return;

                bool activeView = RbActiveView?.IsChecked   == true;
                bool entireMode = RbEntireModel?.IsChecked  == true;
                bool selectLev  = ChkSelectLevel?.IsChecked == true;
                bool selectLink = ChkSelectLink?.IsChecked  == true;

                if (LstLevels != null) 
                    LstLevels.Visibility = selectLev ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                if (CboLinks != null)
                    CboLinks.Visibility = selectLink ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                if (LstLinkLevels != null)
                    LstLinkLevels.Visibility = selectLink ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

                bool incDoors   = ChkDoors?.IsChecked       == true;
                bool incWindows = ChkWindows?.IsChecked     == true;
                bool incPanels  = ChkCurtainPanels?.IsChecked == true;
                bool incWalls   = ChkCurtainWalls?.IsChecked  == true;

                _currentElements = new List<Element>();

                if (activeView)
                {
                    _currentElements = DoorCollector.CollectFromView(_doc, _activeView, incDoors, incWindows, incPanels, incWalls);
                }
                else if (entireMode)
                {
                    _currentElements = DoorCollector.CollectFromDocument(_doc, incDoors, incWindows, incPanels, incWalls);
                }
                else
                {
                    // Custom selection: Combine Local Levels and Link
                    if (selectLev)
                    {
                        var levels = LstLevels?.SelectedItems.Cast<Level>().ToList();
                        if (levels != null && levels.Any())
                        {
                            var localElements = DoorCollector.CollectFromLevels(_doc, levels, incDoors, incWindows, incPanels, incWalls);
                            foreach (var el in localElements) _currentElements.Add(el);
                        }
                    }

                    if (selectLink)
                    {
                        var link = CboLinks?.SelectedItem as RevitLinkInstance;
                        var linkLevels = LstLinkLevels?.SelectedItems.Cast<Level>().ToList();
                        if (link != null)
                        {
                            var linkElements = DoorCollector.CollectFromLink(link, linkLevels, incDoors, incWindows, incPanels, incWalls);
                            foreach (var el in linkElements) _currentElements.Add(el);
                        }
                    }
                }

                var activeDoorTypeIds = new HashSet<ElementId>();
                if (LstDoorTypes?.SelectedItems != null)
                    foreach (TypeViewModel vm in LstDoorTypes.SelectedItems) activeDoorTypeIds.Add(vm.Id);
                    
                var activeWindowTypeIds = new HashSet<ElementId>();
                if (LstWindowTypes?.SelectedItems != null)
                    foreach (TypeViewModel vm in LstWindowTypes.SelectedItems) activeWindowTypeIds.Add(vm.Id);

                var activePanelTypeIds = new HashSet<ElementId>();
                if (LstCurtainPanelTypes?.SelectedItems != null)
                    foreach (TypeViewModel vm in LstCurtainPanelTypes.SelectedItems) activePanelTypeIds.Add(vm.Id);

                var activeWallTypeIds = new HashSet<ElementId>();
                if (LstCurtainWallTypes?.SelectedItems != null)
                    foreach (TypeViewModel vm in LstCurtainWallTypes.SelectedItems) activeWallTypeIds.Add(vm.Id);

                if (incDoors || incWindows || incPanels || incWalls)
                {
                    _currentElements = _currentElements.Where(el => 
                    {
                        if (el.Category.Id.Value == (long)BuiltInCategory.OST_Doors) return activeDoorTypeIds.Contains(el.GetTypeId());
                        if (el.Category.Id.Value == (long)BuiltInCategory.OST_Windows) return activeWindowTypeIds.Contains(el.GetTypeId());
                        if (el.Category.Id.Value == (long)BuiltInCategory.OST_CurtainWallPanels) return activePanelTypeIds.Contains(el.GetTypeId());
                        if (el.Category.Id.Value == (long)BuiltInCategory.OST_Walls) return activeWallTypeIds.Contains(el.GetTypeId());
                        return true;
                    }).ToList();
                }

                int count = _currentElements?.Count ?? 0;

                if (TxtCount != null)
                    TxtCount.Text = count.ToString();

                if (TxtStatus != null)
                    TxtStatus.Text = count == 0
                        ? "No elements found in selected scope."
                        : string.Format("{0} element{1} ready to process.", count, count == 1 ? "" : "s");

                if (TxtSubStatus != null)
                {
                    if (activeView)
                        TxtSubStatus.Text = string.Format("Scope: active view  \u00B7  \u201C{0}\u201D", _activeView?.Name ?? "(none)");
                    else if (entireMode)
                        TxtSubStatus.Text = "Scope: entire model (all levels)";
                    else
                    {
                        var parts = new List<string>();
                        if (selectLev) parts.Add(string.Format("{0} local levels", LstLevels?.SelectedItems.Count ?? 0));
                        if (selectLink) parts.Add(string.Format("link \u201C{0}\u201D", (CboLinks?.SelectedItem as RevitLinkInstance)?.Name ?? "none"));
                        
                        TxtSubStatus.Text = parts.Count > 0 
                            ? "Scope: " + string.Join(" + ", parts)
                            : "Select a scope above";
                    }
                }

                if (BtnCreate != null)
                    BtnCreate.IsEnabled = count > 0;
            }
            catch (Exception)
            {
                if (TxtStatus != null) TxtStatus.Text = "Error calculating elements.";
                // Do not throw to dispatcher
            }
        }

         private void Lst_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RefreshDoorCount();
        }

        private void CboLinks_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                var link = CboLinks.SelectedItem as RevitLinkInstance;
                if (link != null)
                {
                    var linkDoc = link.GetLinkDocument();
                    if (linkDoc != null)
                    {
                        var levels = new FilteredElementCollector(linkDoc)
                            .OfClass(typeof(Level))
                            .OfType<Level>()
                            .OrderBy(l => l.Elevation)
                            .ToList();
                        LstLinkLevels.ItemsSource = levels;
                    }
                    else
                    {
                        LstLinkLevels.ItemsSource = null;
                    }
                }
                RefreshDoorCount();
            }
            catch (Exception)
            {
                // Ignore gracefully
            }
        }

        // ── Button handlers ─────────────────────────────────────────────────────

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            UseActiveView        = RbActiveView.IsChecked      == true;
            UseEntireModel       = RbEntireModel.IsChecked     == true;
            UseLevel             = ChkSelectLevel.IsChecked    == true;
            UseLink              = ChkSelectLink.IsChecked     == true;

            SelectedLevels       = LstLevels.SelectedItems.Cast<Level>().ToList();
            SelectedLinkLevels   = LstLinkLevels.SelectedItems.Cast<Level>().ToList();
            SelectedLink         = CboLinks.SelectedItem       as RevitLinkInstance;

            IncludeDoors         = ChkDoors.IsChecked          == true;
            IncludeWindows       = ChkWindows.IsChecked        == true;
            IncludeCurtainPanels = ChkCurtainPanels.IsChecked  == true;
            IncludeCurtainWalls  = ChkCurtainWalls.IsChecked   == true;
            TagIfc               = ChkTagIfc.IsChecked         == true;
            SkipExisting         = ChkSkipExisting.IsChecked   == true;

            if (LstDoorTypes?.SelectedItems != null)
                foreach (TypeViewModel vm in LstDoorTypes.SelectedItems) SelectedDoorTypeIds.Add(vm.Id);
            if (LstWindowTypes?.SelectedItems != null)
                foreach (TypeViewModel vm in LstWindowTypes.SelectedItems) SelectedWindowTypeIds.Add(vm.Id);
            if (LstCurtainPanelTypes?.SelectedItems != null)
                foreach (TypeViewModel vm in LstCurtainPanelTypes.SelectedItems) SelectedCurtainPanelTypeIds.Add(vm.Id);
            if (LstCurtainWallTypes?.SelectedItems != null)
                foreach (TypeViewModel vm in LstCurtainWallTypes.SelectedItems) SelectedCurtainWallTypeIds.Add(vm.Id);

            SelectedGeometry     = RbEllipse.IsChecked == true 
                                   ? Constants.GeometryType.Ellipse 
                                   : Constants.GeometryType.Rectangle;

            Confirmed            = true;
            DialogResult         = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed    = false;
            DialogResult = false;
            Close();
        }

        private void BtnUndraw_Click(object sender, RoutedEventArgs e)
        {
            UseActiveView        = RbActiveView.IsChecked      == true;
            UseEntireModel       = RbEntireModel.IsChecked     == true;
            UseLevel             = ChkSelectLevel.IsChecked    == true;
            UseLink              = ChkSelectLink.IsChecked     == true;

            SelectedLevels       = LstLevels.SelectedItems.Cast<Level>().ToList();
            SelectedLinkLevels   = LstLinkLevels.SelectedItems.Cast<Level>().ToList();
            SelectedLink         = CboLinks.SelectedItem       as RevitLinkInstance;

            IncludeDoors         = ChkDoors.IsChecked          == true;
            IncludeWindows       = ChkWindows.IsChecked        == true;
            IncludeCurtainPanels = ChkCurtainPanels.IsChecked  == true;
            IncludeCurtainWalls  = ChkCurtainWalls.IsChecked   == true;

            if (LstDoorTypes?.SelectedItems != null)
                foreach (TypeViewModel vm in LstDoorTypes.SelectedItems) SelectedDoorTypeIds.Add(vm.Id);
            if (LstWindowTypes?.SelectedItems != null)
                foreach (TypeViewModel vm in LstWindowTypes.SelectedItems) SelectedWindowTypeIds.Add(vm.Id);
            if (LstCurtainPanelTypes?.SelectedItems != null)
                foreach (TypeViewModel vm in LstCurtainPanelTypes.SelectedItems) SelectedCurtainPanelTypeIds.Add(vm.Id);
            if (LstCurtainWallTypes?.SelectedItems != null)
                foreach (TypeViewModel vm in LstCurtainWallTypes.SelectedItems) SelectedCurtainWallTypeIds.Add(vm.Id);

            UndrawMode           = true;
            Confirmed            = true;
            DialogResult         = true;
            Close();
        }

    }
    
    public class TypeViewModel
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public override string ToString() => Name;
    }
}
