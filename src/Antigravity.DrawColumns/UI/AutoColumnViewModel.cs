using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace Antigravity.DrawColumns.UI
{
    public class AutoColumnViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public List<Level> Levels { get; set; } = new List<Level>();
        public List<FamilySymbol> RectFamilies { get; set; } = new List<FamilySymbol>();
        public List<FamilySymbol> CircleFamilies { get; set; } = new List<FamilySymbol>();

        private Level _selectedLevelBot;
        public Level SelectedLevelBot { get { return _selectedLevelBot; } set { _selectedLevelBot = value; OnPropertyChanged(); } }

        private Level _selectedLevelTop;
        public Level SelectedLevelTop { get { return _selectedLevelTop; } set { _selectedLevelTop = value; OnPropertyChanged(); } }

        private FamilySymbol _selectedRectFamily;
        public FamilySymbol SelectedRectFamily { get { return _selectedRectFamily; } set { _selectedRectFamily = value; OnPropertyChanged(); } }

        private FamilySymbol _selectedCircleFamily;
        public FamilySymbol SelectedCircleFamily { get { return _selectedCircleFamily; } set { _selectedCircleFamily = value; OnPropertyChanged(); } }

        private string _offsetBot = "0";
        public string OffsetBot { get { return _offsetBot; } set { _offsetBot = value; OnPropertyChanged(); } }

        private string _offsetTop = "0";
        public string OffsetTop { get { return _offsetTop; } set { _offsetTop = value; OnPropertyChanged(); } }

        private string _paramB = "b";
        public string ParamB { get { return _paramB; } set { _paramB = value; OnPropertyChanged(); } }

        private string _paramH = "h";
        public string ParamH { get { return _paramH; } set { _paramH = value; OnPropertyChanged(); } }

        private string _paramDia = "D";
        public string ParamDia { get { return _paramDia; } set { _paramDia = value; OnPropertyChanged(); } }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

