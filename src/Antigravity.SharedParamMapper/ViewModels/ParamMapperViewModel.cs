using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Antigravity.SharedParamMapper.Models;
using Antigravity.SharedParamMapper.Services;
using Autodesk.Revit.DB;

namespace Antigravity.SharedParamMapper.ViewModels
{
    public class CategoryItem
    {
        public string Name { get; set; }
        public Category Category { get; set; }
    }

    public class FamilyTypeItem
    {
        public string Name { get; set; }
        public ElementType Type { get; set; }
    }

    public class ParamMapperViewModel : INotifyPropertyChanged
    {
        private readonly Document _doc;
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
        public ICommand SaveCommand { get; }
        public ICommand LoadCommand { get; }

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
                    OnPropertyChanged();
                    LoadParams();
                }
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

        public ParamMapperViewModel(Document doc, ICollection<ElementId> selectedIds)
        {
            _doc = doc;
            _selectedIds = selectedIds;
            _reader = new SharedParamReader();
            _engine = new MappingEngine();
            _store = new MappingTemplateStore();

            SyncCommand = new RelayCommand(ExecuteSync, CanExecuteSync);
            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
            LoadCommand = new RelayCommand(ExecuteLoad);

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
            FamilyTypes.Clear();
            ParamRows.Clear();
            if (SelectedCategory == null) return;

            var types = _reader.GetFamilyTypesInCategory(_doc, (BuiltInCategory)SelectedCategory.Category.Id.IntegerValue);
            foreach (var t in types)
            {
                FamilyTypes.Add(new FamilyTypeItem { Name = $"{t.FamilyName} - {t.Name}", Type = t });
            }
            StatusMessage = $"Loaded {FamilyTypes.Count} family types for {SelectedCategory.Name}.";
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
                    ParamGroup = sp.GetDefinition().ParameterGroup.ToString(),
                    Scope = _reader.GetBindingScope(_doc, sp, SelectedCategory.Category),
                    SelectedSource = MappingSource.Manual,
                    AvailableSourceParams = availableSourceParams
                });
            }
            StatusMessage = $"Loaded {ParamRows.Count} shared parameters.";
        }

        private bool CanExecuteSync() => SelectedFamilyType != null && ParamRows.Any();
        private void ExecuteSync()
        {
            if (SelectedCategory == null || SelectedFamilyType == null) return;

            var mapping = new FamilyTypeMapping
            {
                CategoryName = SelectedCategory.Name,
                FamilyName = SelectedFamilyType.Type.FamilyName,
                TypeName = SelectedFamilyType.Type.Name,
                ElementTypeId = SelectedFamilyType.Type.Id.IntegerValue,
                Rules = ParamRows.Where(p => !string.IsNullOrEmpty(p.MappedValue)).Select(p => new MappingRule
                {
                    SharedParamName = p.ParamName,
                    SharedParamGuid = p.ParamGuid,
                    Scope = p.Scope,
                    Source = p.SelectedSource,
                    FixedValue = p.SelectedSource == MappingSource.Manual ? p.MappedValue : null,
                    SourceParameterName = (p.SelectedSource == MappingSource.BuiltInParam || p.SelectedSource == MappingSource.OtherSharedParam) ? p.MappedValue : null,
                    FormulaExpression = p.SelectedSource == MappingSource.Formula ? p.MappedValue : null
                }).ToList()
            };

            ApplyResult result;
            if (ApplyToAll)
            {
                result = _engine.ApplyToAllInstances(_doc, mapping);
            }
            else
            {
                result = _engine.ApplyToSelectedElements(_doc, mapping, _selectedIds);
            }

            StatusMessage = $"Sync complete. Success: {result.SuccessCount}, Errors: {result.ErrorCount}";
        }

        private bool CanExecuteSave() => SelectedCategory != null && SelectedFamilyType != null && ParamRows.Any(p => !string.IsNullOrEmpty(p.MappedValue));
        private void ExecuteSave()
        {
            try
            {
                var template = _store.Load(_doc.Title);
                template.ProjectName = _doc.Title;

                var existingMapping = template.Mappings.FirstOrDefault(m => m.ElementTypeId == SelectedFamilyType.Type.Id.IntegerValue);
                if (existingMapping != null)
                {
                    template.Mappings.Remove(existingMapping);
                }

                var newMapping = new FamilyTypeMapping
                {
                    CategoryName = SelectedCategory.Name,
                    FamilyName = SelectedFamilyType.Type.FamilyName,
                    TypeName = SelectedFamilyType.Type.Name,
                    ElementTypeId = SelectedFamilyType.Type.Id.IntegerValue,
                    Rules = ParamRows.Where(p => !string.IsNullOrEmpty(p.MappedValue)).Select(p => new MappingRule
                    {
                        SharedParamName = p.ParamName,
                        SharedParamGuid = p.ParamGuid,
                        Scope = p.Scope,
                        Source = p.SelectedSource,
                        FixedValue = p.SelectedSource == MappingSource.Manual ? p.MappedValue : null,
                        SourceParameterName = (p.SelectedSource == MappingSource.BuiltInParam || p.SelectedSource == MappingSource.OtherSharedParam) ? p.MappedValue : null,
                        FormulaExpression = p.SelectedSource == MappingSource.Formula ? p.MappedValue : null
                    }).ToList()
                };

                template.Mappings.Add(newMapping);
                _store.Save(template);
                StatusMessage = "Template saved successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving: {ex.Message}";
            }
        }

        private void ExecuteLoad()
        {
            if (SelectedCategory == null || SelectedFamilyType == null) return;
            try
            {
                var template = _store.Load(_doc.Title);
                var mapping = template.Mappings.FirstOrDefault(m => m.ElementTypeId == SelectedFamilyType.Type.Id.IntegerValue);
                
                if (mapping != null)
                {
                    foreach (var rule in mapping.Rules)
                    {
                        var row = ParamRows.FirstOrDefault(p => p.ParamGuid == rule.SharedParamGuid);
                        if (row != null)
                        {
                            row.SelectedSource = rule.Source;
                            row.MappedValue = rule.Source switch
                            {
                                MappingSource.Manual => rule.FixedValue,
                                MappingSource.BuiltInParam => rule.SourceParameterName,
                                MappingSource.OtherSharedParam => rule.SourceParameterName,
                                MappingSource.Formula => rule.FormulaExpression,
                                _ => string.Empty
                            };
                        }
                    }
                    StatusMessage = "Template loaded successfully.";
                }
                else
                {
                    StatusMessage = "No saved mapping for this family type.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading: {ex.Message}";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
