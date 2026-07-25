using System.Collections.Generic;

namespace Antigravity.Formwork.Core.Models
{
    public class FormworkPanelSystem
    {
        public string SystemId { get; set; }
        public string Name { get; set; }
        
        // Available widths in millimeters, typically sorted descending (e.g., 900, 600, 450, 300)
        public List<double> AvailableWidths { get; set; } = new List<double>();
        
        // Standard panel height in millimeters
        public double StandardHeight { get; set; }

        public static FormworkPanelSystem CreateGenericSystem()
        {
            return new FormworkPanelSystem
            {
                SystemId = "GENERIC_01",
                Name = "Generic Formwork System",
                AvailableWidths = new List<double> { 900, 600, 450, 300 },
                StandardHeight = 3000
            };
        }
    }
}
