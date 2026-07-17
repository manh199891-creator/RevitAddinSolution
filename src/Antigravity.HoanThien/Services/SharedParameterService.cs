using System;
using System.IO;
using Autodesk.Revit.DB;

namespace Antigravity.HoanThien.Services
{
    public class SharedParameterService
    {
        public static void SetupSharedParameters(Document doc, Autodesk.Revit.ApplicationServices.Application app)
        {
            using (Transaction t = new Transaction(doc, "Setup Shared Parameters"))
            {
                t.Start();
                
                string[] paramNames = { "AG_Room_Code", "AG_Wall_Finish", "AG_Floor_Finish" };
                
                CategorySet catSet = app.Create.NewCategorySet();
                Category roomCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Rooms);
                catSet.Insert(roomCat);

                BindingMap map = doc.ParameterBindings;
                bool needsSetup = false;

                foreach (string name in paramNames)
                {
                    // Check if already bound
                    bool bound = false;
                    DefinitionBindingMapIterator it = map.ForwardIterator();
                    it.Reset();
                    while (it.MoveNext())
                    {
                        if (it.Key.Name == name)
                        {
                            bound = true;
                            break;
                        }
                    }

                    if (!bound)
                    {
                        needsSetup = true;
                        break;
                    }
                }

                if (needsSetup)
                {
                    string spFile = app.SharedParametersFilename;
                    if (string.IsNullOrEmpty(spFile) || !File.Exists(spFile))
                    {
                        spFile = Path.Combine(Path.GetTempPath(), "AG_SharedParameters.txt");
                        File.WriteAllText(spFile, "");
                        app.SharedParametersFilename = spFile;
                    }

                    DefinitionFile defFile = app.OpenSharedParameterFile();
                    if (defFile == null)
                    {
                        t.RollBack();
                        return;
                    }

                    DefinitionGroup group = defFile.Groups.get_Item("AG_Automation") ?? defFile.Groups.Create("AG_Automation");

                    foreach (string name in paramNames)
                    {
                        Definition def = group.Definitions.get_Item(name);
                        if (def == null)
                        {
                            ExternalDefinitionCreationOptions opt = new ExternalDefinitionCreationOptions(name, SpecTypeId.String.Text);
                            def = group.Definitions.Create(opt);
                        }

                        InstanceBinding binding = app.Create.NewInstanceBinding(catSet);
                        map.Insert(def, binding, GroupTypeId.IdentityData);
                    }
                }
                t.Commit();
            }
        }
    }
}
