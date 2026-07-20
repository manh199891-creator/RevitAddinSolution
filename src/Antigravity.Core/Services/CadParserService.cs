using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Antigravity.Core.Models;

namespace Antigravity.Core.Services
{
    public class CadParserService
    {
        public List<FoundationData> ExtractFoundationData(ImportInstance cadLink, string layerName)
        {
            var results = new List<FoundationData>();
            var doc = cadLink.Document;
            var geomElem = cadLink.get_Geometry(new Options());

            if (geomElem == null) return results;

            foreach (var geomObj in geomElem)
            {
                if (geomObj is GeometryInstance geomInst)
                {
                    var instanceGeom = geomInst.GetInstanceGeometry();
                    
                    var transform = geomInst.Transform;
                    var origin = transform.Origin;
                    var angle = transform.BasisX.AngleTo(XYZ.BasisX);
                    if (transform.BasisX.Y < 0) angle = -angle;
                    
                    results.Add(new FoundationData 
                    { 
                        Center = origin, 
                        RotationAngle = angle 
                    });
                }
            }

            return results;
        }
    }
}
