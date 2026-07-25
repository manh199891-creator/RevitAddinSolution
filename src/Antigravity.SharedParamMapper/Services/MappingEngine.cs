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
                            bool setSuccess = false;
                            if (rule.Scope == ParamScope.Instance)
                            {
                                setSuccess = SetParameterValue(element, rule, null, result);
                            }
                            else if (rule.Scope == ParamScope.Type)
                            {
                                if (!typeParamsSet && elementType != null)
                                    setSuccess = SetParameterValue(elementType, rule, element, result);
                                else
                                    setSuccess = true; // Already set for type
                            }
                            else if (rule.Scope == ParamScope.Unknown)
                            {
                                var p = element.get_Parameter(rule.SharedParamGuid) ?? element.LookupParameter(rule.SharedParamName);
                                if (p != null)
                                {
                                    setSuccess = SetParameterValue(element, rule, null, result);
                                }
                                else if (!typeParamsSet && elementType != null)
                                {
                                    var tp = elementType.get_Parameter(rule.SharedParamGuid) ?? elementType.LookupParameter(rule.SharedParamName);
                                    if (tp != null)
                                    {
                                        setSuccess = SetParameterValue(elementType, rule, element, result);
                                    }
                                }
                                else if (typeParamsSet)
                                {
                                    setSuccess = true;
                                }
                            }

                            if (!setSuccess)
                            {
                                elementSuccess = false;
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

        private bool SetParameterValue(Element targetElement, MappingRule rule, Element contextElement = null, ApplyResult result = null)
        {
            var param = targetElement.get_Parameter(rule.SharedParamGuid);
            if (param == null)
            {
                param = targetElement.LookupParameter(rule.SharedParamName);
            }

            if (param == null)
            {
                result?.Errors.Add($"Target parameter '{rule.SharedParamName}' not found on element {targetElement.Id}.");
                return false;
            }
            if (param.IsReadOnly)
            {
                result?.Errors.Add($"Target parameter '{rule.SharedParamName}' is read-only on element {targetElement.Id}.");
                return false;
            }

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
                    if (rule.SourceParameterName.StartsWith("<") && rule.SourceParameterName.EndsWith(">"))
                    {
                        valueToSet = EvaluateVirtualParameter(evalElement, rule.SourceParameterName);
                        if (valueToSet == null)
                        {
                            result?.Errors.Add($"Virtual parameter '{rule.SourceParameterName}' returned null on eval element {evalElement.Id}.");
                            return false;
                        }
                    }
                    else
                    {
                        var sourceParam = evalElement.LookupParameter(rule.SourceParameterName);
                        
                        // Fallback: If not found on instance, check if it's a Type parameter
                        if (sourceParam == null)
                        {
                            var typeId = evalElement.GetTypeId();
                            if (typeId != ElementId.InvalidElementId)
                            {
                                var typeElem = evalElement.Document.GetElement(typeId);
                                if (typeElem != null)
                                {
                                    sourceParam = typeElem.LookupParameter(rule.SourceParameterName);
                                }
                            }
                        }

                        if (sourceParam != null)
                        {
                            // Direct transfer if types match (avoids unit string parsing issues)
                        if (param.StorageType == sourceParam.StorageType)
                        {
                            switch (param.StorageType)
                            {
                                case StorageType.Double:
                                    param.Set(sourceParam.AsDouble());
                                    return true;
                                case StorageType.Integer:
                                    param.Set(sourceParam.AsInteger());
                                    return true;
                                case StorageType.ElementId:
                                    param.Set(sourceParam.AsElementId());
                                    return true;
                                case StorageType.String:
                                    param.Set(sourceParam.AsString() ?? string.Empty);
                                    return true;
                            }
                        }

                        if (sourceParam.StorageType == StorageType.String) valueToSet = sourceParam.AsString();
                        else valueToSet = sourceParam.AsValueString() ?? sourceParam.AsDouble().ToString();
                    }
                        else
                        {
                            result?.Errors.Add($"Source parameter '{rule.SourceParameterName}' not found on eval element {evalElement.Id}.");
                            return false;
                        }
                    }
                    break;
                case MappingSource.Formula:
                    valueToSet = _formulaEvaluator.Evaluate(rule.FormulaExpression, evalElement);
                    break;
            }

            if (valueToSet != null)
            {
                if (valueToSet == "")
                {
                    if (param.StorageType == StorageType.String)
                    {
                        param.Set("");
                    }
                    // For non-string parameters, clearing is not universally supported in older Revit API, so we just skip setting without throwing an error
                    return true;
                }

                if (param.StorageType == StorageType.String)
                {
                    param.Set(valueToSet);
                    return true;
                }
                else
                {
                    // Attempt to set by value string if it's double/int (handles units natively)
                    bool set = param.SetValueString(valueToSet);
                    if (!set)
                    {
                        // Fallback to raw double parsing if SetValueString fails (e.g. Number parameter)
                        // Strip out non-numeric characters for safety if needed, but simple TryParse first
                        var cleanStr = new string(valueToSet.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == ',').ToArray());
                        if (double.TryParse(cleanStr, out double dVal))
                        {
                            param.Set(dVal);
                            return true;
                        }
                        result?.Errors.Add($"Failed to parse '{valueToSet}' to match target parameter '{rule.SharedParamName}' type.");
                        return false;
                    }
                    return true;
                }
            }

            return false;
        }
        private string EvaluateVirtualParameter(Element element, string virtualParamName)
        {
            Document doc = element.Document;
            
            ElementType type = element as ElementType;
            if (type == null)
            {
                type = doc.GetElement(element.GetTypeId()) as ElementType;
            }

            if (virtualParamName == "<Family Name>") return type.FamilyName;
            if (virtualParamName == "<Type Name>") return type.Name;

            if (type is HostObjAttributes hostObj)
            {
                var structure = hostObj.GetCompoundStructure();
                if (structure != null)
                {
                    if (virtualParamName == "<Material: Structure>")
                    {
                        for (int i = 0; i < structure.LayerCount; i++)
                        {
                            if (structure.GetLayerFunction(i) == MaterialFunctionAssignment.Structure)
                            {
                                var matId = structure.GetMaterialId(i);
                                if (matId != ElementId.InvalidElementId)
                                {
                                    var mat = doc.GetElement(matId) as Material;
                                    if (mat != null) return mat.Name;
                                }
                            }
                        }
                    }
                    else if (virtualParamName == "<Material: All>")
                    {
                        var names = new List<string>();
                        for (int i = 0; i < structure.LayerCount; i++)
                        {
                            var matId = structure.GetMaterialId(i);
                            if (matId != ElementId.InvalidElementId)
                            {
                                var mat = doc.GetElement(matId) as Material;
                                if (mat != null) names.Add(mat.Name);
                            }
                        }
                        return string.Join(" / ", names);
                    }
                    else if (virtualParamName == "<Material: Finish>")
                    {
                        var names = new List<string>();
                        for (int i = 0; i < structure.LayerCount; i++)
                        {
                            var func = structure.GetLayerFunction(i);
                            if (func == MaterialFunctionAssignment.Finish1 || func == MaterialFunctionAssignment.Finish2)
                            {
                                var matId = structure.GetMaterialId(i);
                                if (matId != ElementId.InvalidElementId)
                                {
                                    var mat = doc.GetElement(matId) as Material;
                                    if (mat != null) names.Add(mat.Name);
                                }
                            }
                        }
                        return string.Join(" / ", names);
                    }
                }
            }

            return null;
        }
    }
}
