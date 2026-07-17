using System.Collections.Generic;

namespace Antigravity.BIMLink.Core.Models
{
    public class StructuralElementDTO
    {
        public string UniqueId { get; set; } // Dùng GUID của Revit
        public ElementType Type { get; set; }
        public string SectionName { get; set; } // Tên tiết diện ĐÃ ĐƯỢC MAP
        public string MaterialName { get; set; }
        
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; } // Null nếu là Wall/Slab

        public List<Point3D> BoundaryPoints { get; set; } // Dành cho Wall, Slab
        
        public InternalForcesDTO Forces { get; set; } 
    }
}
