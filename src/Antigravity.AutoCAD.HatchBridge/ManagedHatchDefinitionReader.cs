using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Antigravity.HatchPatterns.Contracts;

namespace Antigravity.AutoCAD.HatchBridge
{
    public class ManagedHatchDefinitionReader
    {
        private readonly Document _document;

        public ManagedHatchDefinitionReader(Document document)
        {
            _document = document;
        }

        public HatchBridgeResponse ReadHatchDefinitions(HatchBridgeRequest request)
        {
            var response = new HatchBridgeResponse
            {
                RequestId = request.RequestId,
                SourceDocumentFingerprint = request.SourceDocumentFingerprint,
                BridgeVersion = "1.0.0",
                AutoCadVersion = Autodesk.AutoCAD.ApplicationServices.Core.Application.Version.ToString()
            };

            Database db = _document.Database;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (string handleStr in request.EntityHandles)
                {
                    var item = new HatchBridgeResponseItem { Handle = handleStr };
                    try
                    {
                        long handleValue = Convert.ToInt64(handleStr, 16);
                        Handle handle = new Handle(handleValue);
                        
                        if (!db.TryGetObjectId(handle, out ObjectId objId))
                        {
                            item.Status = "EntityNotFound";
                            item.Diagnostics.Add("ObjectId not found from handle.");
                            response.Items.Add(item);
                            continue;
                        }

                        Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (ent is Hatch hatch)
                        {
                            item.PatternName = hatch.PatternName;
                            item.PatternType = hatch.PatternType.ToString();
                            item.PatternScale = hatch.PatternScale;
                            item.PatternAngleRadians = hatch.PatternAngle;
                            
                            item.Layer = hatch.Layer;
                            if (hatch.Color.IsByLayer)
                            {
                                item.Color = "ByLayer";
                            }
                            else if (hatch.Color.IsByBlock)
                            {
                                item.Color = "ByBlock";
                            }
                            else if (hatch.Color.IsByColor)
                            {
                                item.Color = hatch.Color.ColorValue.Name; // typically Hex or named like "ffff0000"
                            }
                            else
                            {
                                item.Color = hatch.Color.ColorIndex.ToString(); // ACI index
                            }
                            item.PatternDouble = hatch.PatternDouble;
                            item.IsSolid = hatch.PatternType == HatchPatternType.PreDefined && hatch.PatternName.ToUpper() == "SOLID";

                            if (item.IsSolid)
                            {
                                item.Status = "Solid";
                                item.DefinitionSemantics = "SolidFill";
                            }
                            else
                            {
                                int numLines = 0;
                                try { numLines = hatch.NumberOfPatternDefinitions; }
                                catch (Exception ex) 
                                {
                                    item.Status = "DefinitionMissing";
                                    item.Diagnostics.Add($"Error reading NumberOfPatternDefinitions: {ex.Message}");
                                }

                                if (numLines > 0)
                                {
                                    for (int i = 0; i < numLines; i++)
                                    {
                                        var defLine = hatch.GetPatternDefinitionAt(i);
                                        var rawLine = new RawPatternLine
                                        {
                                            AngleRadians = defLine.Angle,
                                            BaseX = defLine.BaseX,
                                            BaseY = defLine.BaseY,
                                            OffsetX = defLine.OffsetX,
                                            OffsetY = defLine.OffsetY
                                        };
                                        var dashes = defLine.GetDashes();
                                        foreach (double dash in dashes)
                                        {
                                            rawLine.DashLengths.Add(dash);
                                        }
                                        item.DefinitionLines.Add(rawLine);
                                    }
                                    item.Status = "Extracted";
                                    item.DefinitionSemantics = "RawPatternDefinition";
                                }
                            }
                        }
                        else
                        {
                            item.Status = "Unsupported";
                            item.Diagnostics.Add("Entity is not a Hatch.");
                        }
                    }
                    catch (Exception ex)
                    {
                        item.Status = "Failed";
                        item.Diagnostics.Add(ex.Message);
                    }
                    response.Items.Add(item);
                }
                tr.Commit();
            }

            return response;
        }
    }
}
