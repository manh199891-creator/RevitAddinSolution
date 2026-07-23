using System;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace Antigravity.SharedParamMapper.Services
{
    public class FormulaEvaluator
    {
        // Extracts placeholders like {Length} or {Material}
        private static readonly Regex PlaceholderRegex = new Regex(@"\{([^{}]+)\}");

        public string Evaluate(string expression, Element element)
        {
            if (string.IsNullOrWhiteSpace(expression)) return string.Empty;

            string evaluatedExpression = expression;

            // 1. Resolve parameters
            var matches = PlaceholderRegex.Matches(expression);
            foreach (Match match in matches)
            {
                string paramName = match.Groups[1].Value;
                string paramValue = GetParameterValueAsString(element, paramName);
                
                // If it's a string, we might need to escape quotes if they are inside a string literal, 
                // but for a simple concat evaluation, we'll try to just replace it.
                // A better approach for strings is to just replace if the formula is simple string concat.
                evaluatedExpression = evaluatedExpression.Replace(match.Value, paramValue);
            }

            // 2. Simple evaluation (Very basic arithmetic and string concat)
            // For a robust solution, NCalc or similar would be used.
            // Here we do a very naive string replacement/concat or simple math if it parses to doubles.
            // For now, if there are no mathematical operators, just return the replaced string.
            // E.g. "{Material} - {Location}" -> "Concrete - Floor 1"
            
            // If it contains math operators, try to compute
            if (ContainsMathOperators(evaluatedExpression))
            {
                try
                {
                    var dataTable = new System.Data.DataTable();
                    var result = dataTable.Compute(evaluatedExpression, null);
                    return result?.ToString();
                }
                catch
                {
                    // If compute fails (e.g. contains string literals), fallback to raw evaluated string
                    return evaluatedExpression;
                }
            }

            return evaluatedExpression;
        }

        private bool ContainsMathOperators(string expr)
        {
            return expr.Contains("+") || expr.Contains("-") || expr.Contains("*") || expr.Contains("/");
        }

        private string GetParameterValueAsString(Element element, string paramName)
        {
            Parameter param = element.LookupParameter(paramName);
            if (param == null) return string.Empty;

            switch (param.StorageType)
            {
                case StorageType.String:
                    return param.AsString() ?? string.Empty;
                case StorageType.Double:
                    // Convert to display string to preserve units, or use AsDouble() if we want raw math
                    return param.AsValueString() ?? param.AsDouble().ToString();
                case StorageType.Integer:
                    return param.AsValueString() ?? param.AsInteger().ToString();
                case StorageType.ElementId:
                    return param.AsValueString() ?? param.AsElementId().IntegerValue.ToString();
                default:
                    return string.Empty;
            }
        }
    }
}
