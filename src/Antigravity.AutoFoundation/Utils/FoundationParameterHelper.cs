using Autodesk.Revit.DB;

namespace Antigravity.AutoFoundation.Utils
{
    public static class FoundationParameterHelper
    {
        public static (Parameter Length, Parameter Width) GetDimensions(FamilySymbol symbol)
        {
            var lenNames = new[] { "Length", "L", "Chiều dài", "Chieu dai", "h", "b", "B", "H" };
            var widNames = new[] { "Width", "W", "Chiều rộng", "Chieu rong", "b", "B", "h", "H" };

            Parameter lenParam = symbol.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH);
            Parameter widParam = symbol.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH);

            if (lenParam == null)
            {
                foreach (var name in lenNames)
                {
                    lenParam = symbol.LookupParameter(name);
                    if (lenParam != null && lenParam.StorageType == StorageType.Double) break;
                }
            }

            if (widParam == null)
            {
                foreach (var name in widNames)
                {
                    widParam = symbol.LookupParameter(name);
                    if (widParam != null && widParam.StorageType == StorageType.Double && (lenParam == null || widParam.Id != lenParam.Id)) break;
                }
            }

            return (lenParam, widParam);
        }

        public static bool HasWritableDimensions(FamilySymbol symbol)
        {
            var (lenParam, widParam) = GetDimensions(symbol);
            return lenParam != null && widParam != null
                   && !lenParam.IsReadOnly && !widParam.IsReadOnly
                   && lenParam.StorageType == StorageType.Double
                   && widParam.StorageType == StorageType.Double;
        }
    }
}
