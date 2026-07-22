
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Antigravity.AutoFoundation.Services
{
    public class CadInteropService
    {
        private dynamic _acadApp;
        private dynamic _acadDoc;

        public bool Connect()
        {
            try
            {
                _acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                _acadDoc = _acadApp.ActiveDocument;
                return true;
        }
            catch (Exception ex)
            {
                throw new Exception("Không thể kết nối với AutoCAD. Đảm bảo AutoCAD đang mở.", ex);
        }
    }

        public void SetOriginFromRevitPoint(Autodesk.Revit.DB.XYZ revitOriginFeet)
        {
            try
            {
                _acadApp.Visible = true;
                dynamic utility = _acadDoc.Utility;
                utility.Prompt("\nClick the corresponding origin point on the AutoCAD drawing: ");
                dynamic cadPt = utility.GetPoint(Type.Missing, "\nSelect CAD origin: ");
                
                double cadX = (double)cadPt[0]; // mm
                double cadY = (double)cadPt[1]; // mm

                Autodesk.Revit.DB.XYZ offset = new Autodesk.Revit.DB.XYZ(
                    revitOriginFeet.X - cadX / 304.8,
                    revitOriginFeet.Y - cadY / 304.8,
                    0);
                    
                Antigravity.Core.Services.CoordinateService.SetOriginOffset(offset);
        }
            catch (Exception ex)
            {
                throw new Exception("Error getting origin point from CAD: " + ex.Message);
        }
    }

    }
}


