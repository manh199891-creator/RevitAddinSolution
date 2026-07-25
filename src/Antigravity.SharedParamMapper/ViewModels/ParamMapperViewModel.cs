using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Antigravity.SharedParamMapper.Models;
using Antigravity.SharedParamMapper.Services;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Antigravity.SharedParamMapper.ViewModels
{
    public class CategoryItem
    {
        public string Name { get; set; }
        public Category Category { get; set; }
    }

    public class FamilyTypeItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; set; }
        public ElementType Type { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ParamMapperViewModel : INotifyPropertyChanged
    {
        private readonly Document _doc;
        private readonly UIApplication _uiapp;
        private readonly ActionEventHandler _handler;
        private readonly SharedParamReader _reader;
        private readonly MappingEngine _engine;
        private readonly MappingTemplateStore _store;
        private readonly ICollection<ElementId> _selectedIds;

        private CategoryItem _selectedCategory;
        private FamilyTypeItem _selectedFamilyType;
        private string _statusMessage;
        private bool _applyToAll = true;

        public ObservableCollection<CategoryItem> Categories { get; } = new ObservableCollection<CategoryItem>();
        public ObservableCollection<FamilyTypeItem> FamilyTypes { get; } = new ObservableCollection<FamilyTypeItem>();
        public ObservableCollection<ParamRowViewModel> ParamRows { get; } = new ObservableCollection<ParamRowViewModel>();
        
        public List<MappingSource> MappingSourceOptions { get; } = Enum.GetValues(typeof(MappingSource)).Cast<MappingSource>().ToList();

        public ICommand SyncCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand LoadTxtCommand { get; }

        public CategoryItem SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    _selectedCategory = value;
                    OnPropertyChanged();
                    LoadFamilyTypes();
                }
            }
        }

        public FamilyTypeItem SelectedFamilyType
        {
            get => _selectedFamilyType;
            set
            {
                if (_selectedFamilyType != value)
                {
                    _selectedFamilyType = value;
                    if (_selectedFamilyType != null)
                        _selectedFamilyType.IsSelected = true;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedFamilyTypesText));
                    LoadParams();
                }
            }
        }

        public string SelectedFamilyTypesText
        {
            get
            {
                var selected = FamilyTypes.Where(t => t.IsSelected).Select(t => t.Name).ToList();
                if (!selected.Any()) return "Select Family Types...";
                if (selected.Count == 1) return selected.First();
                return $"{selected.Count} types selected";
            }
        }

        public bool ApplyToAll
        {
            get => _applyToAll;
            set { _applyToAll = value; OnPropertyChanged(); OnPropertyChanged(nameof(ApplyToSelected)); }
        }

        public bool ApplyToSelected
        {
            get => !_applyToAll;
            set { _applyToAll = !value; OnPropertyChanged(); OnPropertyChanged(nameof(ApplyToAll)); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ParamMapperViewModel(UIApplication uiapp, ICollection<ElementId> selectedIds, ActionEventHandler handler)
        {
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;
            _selectedIds = selectedIds;
            _handler = handler;
            _reader = new SharedParamReader();
            _engine = new MappingEngine();
            _store = new MappingTemplateStore();

            SyncCommand = new RelayCommand(ExecuteSync, CanExecuteSync);
            ExportCommand = new RelayCommand(ExecuteExport);
            ImportCommand = new RelayCommand(ExecuteImport);
            LoadTxtCommand = new RelayCommand(ExecuteLoadTxt);

            if (_selectedIds != null && _selectedIds.Any())
            {
                ApplyToSelected = true;
            }

            LoadCategories();
        }

        private void LoadCategories()
        {
            Categories.Clear();
            var cats = _reader.GetAllCategories(_doc);
            foreach (var c in cats)
            {
                Categories.Add(new CategoryItem { Name = c.Name, Category = c });
            }
            StatusMessage = $"Loaded {Categories.Count} categories.";
        }

        private void LoadFamilyTypes()
        {
            SelectedFamilyType = null;
            FamilyTypes.Clear();
            ParamRows.Clear();
            if (SelectedCategory == null) return;

            var types = _reader.GetFamilyTypesInCategory(_doc, SelectedCategory.Category.Id);
            foreach (var t in types)
            {
                var item = new FamilyTypeItem { Name = $"{t.FamilyName} - {t.Name}", Type = t };
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(FamilyTypeItem.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedFamilyTypesText));
                        if (item.IsSelected && SelectedFamilyType == null) SelectedFamilyType = item;
                    }
                };
                FamilyTypes.Add(item);
            }
            StatusMessage = $"Loaded {FamilyTypes.Count} family types for {SelectedCategory.Name}.";
            OnPropertyChanged(nameof(SelectedFamilyTypesText));
        }

        private void LoadParams()
        {
            ParamRows.Clear();
            if (SelectedCategory == null || SelectedFamilyType == null) return;

            var sharedParams = _reader.GetBoundSharedParams(_doc, SelectedCategory.Category);
            var availableSourceParams = _reader.GetAvailableSourceParams(_doc, SelectedFamilyType.Type);

            foreach (var sp in sharedParams)
            {
                ParamRows.Add(new ParamRowViewModel
                {
                    ParamName = sp.Name,
                    ParamGuid = sp.GuidValue,
                    ParamGroup = sp.GetDefinition().GetGroupTypeId().TypeId,
                    Scope = _reader.GetBindingScope(_doc, sp, SelectedCategory.Category),
                    SelectedSource = MappingSource.Manual,
                    AvailableSourceParams = availableSourceParams
                });
            }
            
            // Auto-load history
            var template = _store.Load(_doc.Title);
            var mapping = template.Mappings.FirstOrDefault(m => m.CategoryName == SelectedCategory.Name);
            if (mapping != null)
            {
                foreach (var rule in mapping.Rules)
                {
                    var row = ParamRows.FirstOrDefault(p => p.ParamGuid == rule.SharedParamGuid);
                    if (row != null)
                    {
                        row.MappedValue = rule.Source == MappingSource.Manual ? rule.FixedValue : (rule.Source == MappingSource.Formula ? rule.FormulaExpression : rule.SourceParameterName);
                    }
                }
            }

            StatusMessage = $"Loaded {ParamRows.Count} shared parameters.";
        }

        private MappingSource InferSource(string mappedValue, List<string> availableParams)
        {
            if (string.IsNullOrWhiteSpace(mappedValue)) return MappingSource.Manual;
            if (availableParams != null && availableParams.Contains(mappedValue)) return MappingSource.OtherSharedParam;
            if (mappedValue.Contains("{") && mappedValue.Contains("}")) return MappingSource.Formula;
            return MappingSource.Manual;
        }

        private bool CanExecuteSync() => SelectedFamilyType != null && ParamRows.Any();
        private void ExecuteSync()
        {
            if (SelectedCategory == null || SelectedFamilyType == null) return;

            var typesToApply = FamilyTypes.Where(t => t.IsSelected).ToList();
            if (!typesToApply.Any()) typesToApply.Add(SelectedFamilyType);

            _handler.Raise(app => 
            {
                int totalSuccess = 0;
                int totalError = 0;
                var allErrors = new List<string>();

                foreach (var familyType in typesToApply)
                {
                    var mapping = new FamilyTypeMapping
                    {
                        CategoryName = SelectedCategory.Name,
                        FamilyName = familyType.Type.FamilyName,
                        TypeName = familyType.Type.Name,
                        ElementTypeId = familyType.Type.Id.IntegerValue,
                        Rules = ParamRows.Select(p =>
                        {
                            var inferredSource = InferSource(p.MappedValue, p.AvailableSourceParams);
                            return new MappingRule
                            {
                                SharedParamName = p.ParamName,
                                SharedParamGuid = p.ParamGuid,
                                Scope = p.Scope,
                                Source = inferredSource,
                                FixedValue = p.MappedValue ?? "",
                                SourceParameterName = p.MappedValue ?? "",
                                FormulaExpression = p.MappedValue ?? ""
                            };
                        }).ToList()
                    };

                    ApplyResult result;
                    if (ApplyToAll)
                    {
                        result = _engine.ApplyToAllInstances(app.ActiveUIDocument.Document, mapping);
                    }
                    else
                    {
                        var currentSelection = app.ActiveUIDocument.Selection.GetElementIds();
                        result = _engine.ApplyToSelectedElements(app.ActiveUIDocument.Document, mapping, currentSelection);
                    }

                    totalSuccess += result.SuccessCount;
                    totalError += result.ErrorCount;
                    if (result.Errors.Any())
                    {
                        allErrors.AddRange(result.Errors);
                    }
                }

                ExecuteSave(); // Auto-save history after sync
                StatusMessage = $"Sync complete for {typesToApply.Count} types. Success: {totalSuccess}, Errors: {totalError}";
                
                if (allErrors.Any())
                {
                    var errorSummary = string.Join("\n", allErrors.Take(15));
                    if (allErrors.Count > 15) errorSummary += $"\n... and {allErrors.Count - 15} more errors.";
                    MessageBox.Show($"Sync completed with {totalError} errors:\n\n{errorSummary}", "Sync Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });
        }

        private bool CanExecuteSave() => SelectedCategory != null && SelectedFamilyType != null && ParamRows.Any(p => !string.IsNullOrEmpty(p.MappedValue));
        private void ExecuteSave()
        {
            if (SelectedCategory == null || SelectedFamilyType == null) return;
            try
            {
                var template = _store.Load(_doc.Title);
                template.ProjectName = _doc.Title;

                var existingMappings = template.Mappings.Where(m => m.CategoryName == SelectedCategory.Name).ToList();
                foreach (var m in existingMappings) template.Mappings.Remove(m);

                var newMapping = new FamilyTypeMapping
                {
                    CategoryName = SelectedCategory.Name,
                    FamilyName = "All",
                    TypeName = "All",
                    ElementTypeId = 0,
                    Rules = ParamRows.Select(p =>
                    {
                        var inferredSource = InferSource(p.MappedValue, p.AvailableSourceParams);
                        return new MappingRule
                        {
                            SharedParamName = p.ParamName,
                            SharedParamGuid = p.ParamGuid,
                            Scope = p.Scope,
                            Source = inferredSource,
                            FixedValue = p.MappedValue ?? "",
                            SourceParameterName = p.MappedValue ?? "",
                            FormulaExpression = p.MappedValue ?? ""
                        };
                    }).ToList()
                };

                template.Mappings.Add(newMapping);

                _store.Save(template);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error auto-saving history: {ex.Message}";
            }
        }

        private void ExecuteExport()
        {
            ExecuteSave(); // Ensure current state is saved to memory before exporting
            try
            {
                var template = _store.Load(_doc.Title);
                if (template == null || template.Mappings == null || !template.Mappings.Any())
                {
                    MessageBox.Show("No mappings to export.");
                    return;
                }

                var availableCategories = template.Mappings.Select(m => m.CategoryName).Distinct().ToList();
                var exportWindow = new Views.ExportOptionsWindow(availableCategories);
                
                if (Application.Current != null)
                {
                    var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                    if (activeWindow != null)
                    {
                        exportWindow.Owner = activeWindow;
                    }
                }

                if (exportWindow.ShowDialog() != true) return;

                var selectedCategories = exportWindow.Categories.Where(c => c.IsSelected).Select(c => c.Name).ToList();

                // Filter template to only include selected categories
                template.Mappings = template.Mappings.Where(m => selectedCategories.Contains(m.CategoryName)).ToList();

                var dialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    Title = "Export Mapping Template",
                    FileName = selectedCategories.Count == 1 
                        ? $"{_doc.Title}_{selectedCategories[0]}_MappingTemplate.json" 
                        : $"{_doc.Title}_Selected_MappingTemplate.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    _store.ExportToFile(template, dialog.FileName);
                    StatusMessage = "Template exported successfully.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting: {ex.Message}");
            }
        }

        private void ExecuteImport()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    Title = "Import Mapping Template"
                };

                if (dialog.ShowDialog() == true)
                {
                    var importedTemplate = _store.ImportFromFile(dialog.FileName);
                    if (importedTemplate == null || importedTemplate.Mappings == null || !importedTemplate.Mappings.Any())
                    {
                        MessageBox.Show("Selected file does not contain valid mappings.");
                        return;
                    }

                    var availableCategories = importedTemplate.Mappings.Select(m => m.CategoryName).Distinct().ToList();
                    var importWindow = new Views.ExportOptionsWindow(availableCategories, "Import Options", "Select categories to import:", "Import");
                    
                    if (Application.Current != null)
                    {
                        var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                        if (activeWindow != null) importWindow.Owner = activeWindow;
                    }

                    if (importWindow.ShowDialog() != true) return;
                    
                    var selectedCategories = importWindow.Categories.Where(c => c.IsSelected).Select(c => c.Name).ToList();
                    var currentTemplate = _store.Load(_doc.Title);
                    
                    // Merge imported mappings into current project history
                    foreach (var mapping in importedTemplate.Mappings.Where(m => selectedCategories.Contains(m.CategoryName)))
                    {
                        var existingMappings = currentTemplate.Mappings.Where(m => m.CategoryName == mapping.CategoryName).ToList();
                        foreach (var m in existingMappings) currentTemplate.Mappings.Remove(m);
                        
                        mapping.ElementTypeId = 0;
                        mapping.FamilyName = "All";
                        mapping.TypeName = "All";
                        
                        currentTemplate.Mappings.Add(mapping);
                    }
                    _store.Save(currentTemplate);
                    
                    // Refresh UI
                    if (SelectedFamilyType != null)
                    {
                        LoadParams();
                    }
                    StatusMessage = "Template imported successfully.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing: {ex.Message}");
            }
        }

        private void ExecuteLoadTxt()
        {
            if (SelectedCategory == null || SelectedFamilyType == null)
            {
                StatusMessage = "Please select Category and Family Type first.";
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                Title = "Select Shared Parameter File"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ParamRows.Clear();
                    var lines = File.ReadAllLines(dialog.FileName);
                    var availableSourceParams = _reader.GetAvailableSourceParams(_doc, SelectedFamilyType.Type);

                    int count = 0;
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (line.StartsWith("PARAM\t") || line.StartsWith("PARAM "))
                        {
                            var parts = line.Split('\t');
                            if (parts.Length >= 4) // PARAM, GUID, NAME, DATATYPE
                            {
                                if (Guid.TryParse(parts[1], out Guid guid))
                                {
                                    ParamRows.Add(new ParamRowViewModel
                                    {
                                        ParamName = parts[2],
                                        ParamGuid = guid,
                                        ParamGroup = parts.Length > 5 ? parts[5] : "Unknown",
                                        Scope = ParamScope.Unknown,
                                        SelectedSource = MappingSource.Manual,
                                        AvailableSourceParams = availableSourceParams
                                    });
                                    count++;
                                }
                            }
                        }
                    }
                    StatusMessage = $"Loaded {count} parameters from TXT file.";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error loading TXT: {ex.Message}";
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
