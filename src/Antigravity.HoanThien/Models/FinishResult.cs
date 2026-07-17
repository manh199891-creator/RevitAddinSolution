using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Antigravity.HoanThien.Models
{
    public class FinishResult
    {
        public int RoomsProcessed { get; set; }
        public List<ElementId> CreatedWalls { get; set; } = new List<ElementId>();
        public List<ElementId> CreatedFloors { get; set; } = new List<ElementId>();
        public List<string> Errors { get; set; } = new List<string>();
        public int Skipped { get; set; }
        
        public bool Success => Errors.Count == 0;
    }
}
