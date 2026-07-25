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
            foreach (Category c in doc.Settings.Categories)
            {
                if (c.CategoryType == CategoryType.Model && c.AllowsBoundParameters && c.Parent == null)
                {
                    // Exclude Detail Items (2D)
                    if (c.Id.IntegerValue != (int)BuiltInCategory.OST_DetailComponents)
                    {
                        categories.Add(c);
                    }
                }
            }
            return categories.OrderBy(c => c.Name).ToList();
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

        public List<ElementType> GetFamilyTypesInCategory(Document doc, ElementId categoryId)
        {
            var types = new FilteredElementCollector(doc)
                .OfCategoryId(categoryId)
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

            // Fallback: if no instance of this specific type exists, grab ANY instance of this category
            if (sampleInstance == null)
            {
                sampleInstance = new FilteredElementCollector(doc)
                    .OfCategoryId(type.Category.Id)
                    .WhereElementIsNotElementType()
                    .FirstOrDefault();
            }

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

            if (type is HostObjAttributes)
            {
                paramNames.Add("<Material: Structure>");
                paramNames.Add("<Material: Finish>");
                paramNames.Add("<Material: All>");
            }

            paramNames.Add("<Family Name>");
            paramNames.Add("<Type Name>");

            return paramNames.OrderBy(n => n).ToList();
        }
    }
}
