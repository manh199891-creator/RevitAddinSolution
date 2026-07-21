using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace Antigravity.AutoFoundation.UI
{
    public class AutoFoundationViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public List<Level> Levels { get; set; } = new List<Level>();
        public List<FamilySymbol> FoundationFamilies { get; set; } = new List<FamilySymbol>();

        private Level _selectedLevel;
        public Level SelectedLevel { get { return _selectedLevel; } set { _selectedLevel = value; OnPropertyChanged(); } }

        private FamilySymbol _selectedFamily;
        public FamilySymbol SelectedFamily { get { return _selectedFamily; } set { _selectedFamily = value; OnPropertyChanged(); } }

        private string _cadLayer = "S-FND";
        public string CadLayer { get { return _cadLayer; } set { _cadLayer = value; OnPropertyChanged(); } }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
