using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Antigravity.HoanThien.Models;
using Antigravity.HoanThien.Services;
using Antigravity.Core.Services;

namespace Antigravity.HoanThien.Handlers
{
    public class HoanThienHandler
    {
        public FinishResult Execute(UIApplication app, UI.HoanThienViewModel viewModel)
        {
            var config = viewModel.Config;
            var result = new FinishResult();
            var doc = app.ActiveUIDocument.Document;

            // Guard against volume computation off (R3)
            var areaVolumeSettings = AreaVolumeSettings.GetAreaVolumeSettings(doc);
            if (areaVolumeSettings != null && !areaVolumeSettings.ComputeVolumes)
            {
                var tdResult = TaskDialog.Show("Vilai Viet", "Tính năng 'Compute Volumes' (tính thể tích) đang tắt.\nĐiều này có thể làm sai lệch ranh giới lớp hoàn thiện.\n\nBạn có muốn Add-in tự động bật nó lên không?", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (tdResult == TaskDialogResult.Yes)
                {
                    using (var tx = new Transaction(doc, "Bật Compute Volumes"))
                    {
                        tx.Start();
                        areaVolumeSettings.ComputeVolumes = true;
                        tx.Commit();
                    }
                }
                else
                {
                    result.Errors.Add("Compute Volumes is turned off.");
                    return result;
                }
            }

            List<Room> rooms;
            if (viewModel.UsePreSelection)
            {
                var selectedIds = app.ActiveUIDocument.Selection.GetElementIds();
                rooms = selectedIds
                    .Select(id => doc.GetElement(id) as Room)
                    .Where(r => r != null && r.Area > 0)
                    .ToList();
            }
            else
            {
                rooms = new FilteredElementCollector(doc, doc.ActiveView.Id)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .Cast<Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                var selectedNames = viewModel.AvailableRoomNames
                    .Where(x => x.IsSelected && x.Name != "[Tất cả phòng]")
                    .Select(x => x.Name)
                    .ToList();

                if (selectedNames.Count > 0)
                {
                    rooms = rooms
                        .Where(r => selectedNames.Contains(r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString()))
                        .ToList();
                }
            }

            if (config.UseSharedParameters)
            {
                SharedParameterService.SetupSharedParameters(doc, app.Application);
            }

            using (var tx = new Transaction(doc, "Tạo Lớp Hoàn Thiện"))
            {
                tx.Start();

                WallType wallType = null;
                if (config.CreateWall && config.SelectedWallTypeId != null && config.SelectedWallTypeId != ElementId.InvalidElementId)
                {
                    wallType = doc.GetElement(config.SelectedWallTypeId) as WallType;
                }

                FloorType floorType = null;
                if (config.CreateFloor && config.SelectedFloorTypeId != null && config.SelectedFloorTypeId != ElementId.InvalidElementId)
                {
                    floorType = doc.GetElement(config.SelectedFloorTypeId) as FloorType;
                }

                if (config.EnableRoomCoding)
                {
                    RoomCodingService.CodeRooms(doc, rooms, config.RoomCodePrefix, config.UseSharedParameters);
                }

                foreach (var room in rooms)
                {
                    // R5: Double-run duplicate check
                    bool hasExisting = new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .WhereElementIsNotElementType()
                        .Where(e => (e is Wall || e is Floor) && e.LookupParameter("AG_RoomNumber")?.AsString() == room.Number && e.LookupParameter("AG_FinishType")?.AsString() == config.LayerName)
                        .Any();
                    
                    if (hasExisting)
                    {
                        result.Skipped++;
                        continue;
                    }

                    if (config.CreateWall && wallType != null)
                    {
                        var curves = RoomBoundaryService.GetBoundarySegments(room, config);
                        double height = HeightCalculator.GetFinishHeight(room, config.HeightOffsetAboveCeilingMm);

                        foreach (var curve in curves)
                        {
                            var newWall = FinishWallBuilder.BuildFinishWall(doc, curve, config, room, height, wallType);
                            if (newWall != null)
                            {
                                result.CreatedWalls.Add(newWall.Id);
                            }
                        }
                    }

                    if (config.CreateFloor && floorType != null)
                    {
                        var newFloor = FinishFloorBuilder.BuildFinishFloor(doc, room, config, room.LevelId, floorType);
                        if (newFloor != null)
                        {
                            result.CreatedFloors.Add(newFloor.Id);
                        }
                    }

                    // Automation: Write back finish types to Room parameters
                    if (config.CreateWall && wallType != null)
                    {
                        if (config.UseSharedParameters)
                        {
                            room.LookupParameter("AG_Wall_Finish")?.Set(wallType.Name);
                        }
                        else
                        {
                            room.get_Parameter(BuiltInParameter.ROOM_FINISH_WALL)?.Set(wallType.Name);
                        }
                    }

                    if (config.CreateFloor && floorType != null)
                    {
                        if (config.UseSharedParameters)
                        {
                            room.LookupParameter("AG_Floor_Finish")?.Set(floorType.Name);
                        }
                        else
                        {
                            room.get_Parameter(BuiltInParameter.ROOM_FINISH_FLOOR)?.Set(floorType.Name);
                        }
                    }

                    result.RoomsProcessed++;
                }

                tx.Commit();
            }

            AppLogger.Information($"Hoan Thien created: {result.CreatedWalls.Count} walls, {result.CreatedFloors.Count} floors in {result.RoomsProcessed} rooms.");
            return result;
        }
    }
}
