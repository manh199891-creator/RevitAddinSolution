using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Antigravity.SharedParamMapper.Models
{
    public class ParamRowViewModel : INotifyPropertyChanged
    {
        private MappingSource _selectedSource;
        private string _mappedValue;

        public string ParamName { get; set; }
        public Guid ParamGuid { get; set; }
        public string ParamGroup { get; set; }
        public ParamScope Scope { get; set; }
        public string CurrentValue { get; set; }

        public MappingSource SelectedSource
        {
            get => _selectedSource;
            set
            {
                if (_selectedSource != value)
                {
                    _selectedSource = value;
                    OnPropertyChanged();
                    // Reset mapped value when source changes
                    MappedValue = string.Empty;
                }
            }
        }

        public string MappedValue
        {
            get => _mappedValue;
            set
            {
                if (_mappedValue != value)
                {
                    _mappedValue = value;
                    OnPropertyChanged();
                }
            }
        }

        // List of parameter names available for BuiltInParam or OtherSharedParam mapping
        public List<string> AvailableSourceParams { get; set; } = new List<string>();

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
