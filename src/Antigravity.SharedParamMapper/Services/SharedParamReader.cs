using System;
using System.Collections.Generic;
using System.Linq;
using Antigravity.SharedParamMapper.Models;
using Autodesk.Revit.DB;

namespace Antigravity.SharedParamMapper.Services
{
    public class SharedParamReader
    {
        public List<Category> GetAllCategories(Document doc)
        {
            var categories = new List<Category>();
            // Use an arbitrary view or doc settings, or just iterate Categories
            // Note: Not all categories have elements. We can filter categories that actually have elements in the model
            var elementCats = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Select(e => e.Category)
                .Where(c => c != null && c.CategoryType == CategoryType.Model)
                .GroupBy(c => c.Id.IntegerValue)
                .Select(g => g.First())
                .OrderBy(c => c.Name)
                .ToList();

            return elementCats;
        }

        public List<SharedParameterElement> GetBoundSharedParams(Document doc, Category category)
        {
            var boundSharedParams = new List<SharedParameterElement>();
            if (category == null) return boundSharedParams;

            var iterator = doc.ParameterBindings.ForwardIterator();
            while (iterator.MoveNext())
            {
                var binding = iterator.Current as ElementBinding;
                if (binding != null && binding.Categories.Contains(category))
                {
                    var def = iterator.Key as InternalDefinition;
                    if (def != null)
                    {
                        // Check if it's a shared parameter
                            var sharedParam = new FilteredElementCollector(doc)
                                .OfClass(typeof(SharedParameterElement))
                                .Cast<SharedParameterElement>()
                                .FirstOrDefault(sp => sp.Id == def.Id);

                        if (sharedParam != null)
                        {
                            boundSharedParams.Add(sharedParam);
                        }
                    }
                }
            }
            return boundSharedParams;
        }

        public ParamScope GetBindingScope(Document doc, SharedParameterElement sharedParam, Category category)
        {
            var iterator = doc.ParameterBindings.ForwardIterator();
            while (iterator.MoveNext())
            {
                var binding = iterator.Current as ElementBinding;
                if (binding != null && binding.Categories.Contains(category))
                {
                    var def = iterator.Key as InternalDefinition;
                    if (def != null && sharedParam.Id == def.Id)
                    {
                        return binding is TypeBinding ? ParamScope.Type : ParamScope.Instance;
                    }
                }
            }
            return ParamScope.Instance; // Default fallback
        }

        public List<ElementType> GetFamilyTypesInCategory(Document doc, BuiltInCategory bic)
        {
            var types = new FilteredElementCollector(doc)
                .OfCategory(bic)
                .WhereElementIsElementType()
                .Cast<ElementType>()
                .OrderBy(t => t.FamilyName)
                .ThenBy(t => t.Name)
                .ToList();
            return types;
        }

        public List<string> GetAvailableSourceParams(Document doc, ElementType type)
        {
            var paramNames = new HashSet<string>();
            
            // Get a sample instance to read instance parameters
            var sampleInstance = new FilteredElementCollector(doc)
                .OfCategoryId(type.Category.Id)
                .WhereElementIsNotElementType()
                .FirstOrDefault(e => e.GetTypeId() == type.Id);

            if (sampleInstance != null)
            {
                foreach (Parameter p in sampleInstance.Parameters)
                {
                    paramNames.Add(p.Definition.Name);
                }
            }

            // Also add type parameters
            foreach (Parameter p in type.Parameters)
            {
                paramNames.Add(p.Definition.Name);
            }

            return paramNames.OrderBy(n => n).ToList();
        }
    }
}
