using System;
using System.Collections.Generic;
using System.Linq;
using Antigravity.SharedParamMapper.Models;
using Autodesk.Revit.DB;

namespace Antigravity.SharedParamMapper.Services
{
    public class ApplyResult
    {
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class MappingEngine
    {
        private readonly FormulaEvaluator _formulaEvaluator = new FormulaEvaluator();

        public ApplyResult ApplyToSelectedElements(Document doc, FamilyTypeMapping mapping, ICollection<ElementId> selectedIds)
        {
            var elements = selectedIds.Select(id => doc.GetElement(id))
                                      .Where(e => e != null && e.GetTypeId().IntegerValue == mapping.ElementTypeId)
                                      .ToList();
            return Apply(doc, mapping, elements);
        }

        public ApplyResult ApplyToAllInstances(Document doc, FamilyTypeMapping mapping)
        {
            var typeId = new ElementId(mapping.ElementTypeId);
            var elementType = doc.GetElement(typeId) as ElementType;
            if (elementType == null) return new ApplyResult { ErrorCount = 1, Errors = { "Family Type not found." } };

            var elements = new FilteredElementCollector(doc)
                .OfCategoryId(elementType.Category.Id)
                .WhereElementIsNotElementType()
                .Where(e => e.GetTypeId() == typeId)
                .ToList();

            return Apply(doc, mapping, elements);
        }

        private ApplyResult Apply(Document doc, FamilyTypeMapping mapping, List<Element> elements)
        {
            var result = new ApplyResult();
            if (!elements.Any()) return result;

            using (Transaction t = new Transaction(doc, "Apply Param Mapping"))
            {
                t.Start();
                
                // Track if we already set Type params to avoid redundant sets
                bool typeParamsSet = false;
                var elementType = doc.GetElement(new ElementId(mapping.ElementTypeId)) as ElementType;

                foreach (var element in elements)
                {
                    bool elementSuccess = true;
                    foreach (var rule in mapping.Rules)
                    {
                        try
                        {
                            if (rule.Scope == ParamScope.Instance)
                            {
                                SetParameterValue(element, rule);
                            }
                            else if (rule.Scope == ParamScope.Type && !typeParamsSet && elementType != null)
                            {
                                SetParameterValue(elementType, rule, element); // Use instance context for formula if needed
                            }
                        }
                        catch (Exception ex)
                        {
                            elementSuccess = false;
                            result.Errors.Add($"Failed to set {rule.SharedParamName} on element {element.Id}: {ex.Message}");
                        }
                    }
                    typeParamsSet = true; // Set type params only once per run

                    if (elementSuccess) result.SuccessCount++;
                    else result.ErrorCount++;
                }
                
                t.Commit();
            }

            return result;
        }

        private void SetParameterValue(Element targetElement, MappingRule rule, Element contextElement = null)
        {
            var param = targetElement.get_Parameter(rule.SharedParamGuid);
            if (param == null)
            {
                param = targetElement.LookupParameter(rule.SharedParamName);
            }

            if (param == null || param.IsReadOnly) return;

            string valueToSet = null;
            // Use contextElement (Instance) for formula/source eval if setting a Type param, 
            // otherwise use targetElement
            Element evalElement = contextElement ?? targetElement;

            switch (rule.Source)
            {
                case MappingSource.Manual:
                    valueToSet = rule.FixedValue;
                    break;
                case MappingSource.BuiltInParam:
                case MappingSource.OtherSharedParam:
                    var sourceParam = evalElement.LookupParameter(rule.SourceParameterName);
                    if (sourceParam != null)
                    {
                        if (sourceParam.StorageType == StorageType.String) valueToSet = sourceParam.AsString();
                        else valueToSet = sourceParam.AsValueString() ?? sourceParam.AsDouble().ToString();
                    }
                    break;
                case MappingSource.Formula:
                    valueToSet = _formulaEvaluator.Evaluate(rule.FormulaExpression, evalElement);
                    break;
            }

            if (valueToSet != null)
            {
                if (param.StorageType == StorageType.String)
                {
                    param.Set(valueToSet);
                }
                else
                {
                    // Attempt to set by value string if it's double/int (handles units natively)
                    bool set = param.SetValueString(valueToSet);
                    if (!set && double.TryParse(valueToSet, out double dVal))
                    {
                        // Fallback to raw double if SetValueString fails
                        param.Set(dVal);
                    }
                }
            }
        }
    }
}
