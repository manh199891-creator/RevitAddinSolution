using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.Autojoin.Models;
using Antigravity.Core.Services;

namespace Antigravity.Autojoin.Services
{
    /// <summary>
    /// Dynamic Model Update (DMU) — tự động join geometry khi phần tử
    /// được tạo mới hoặc geometry thay đổi.
    /// </summary>
    public class AutoJoinUpdater : IUpdater
    {
        // AddInId cố định theo manifest file
        private static readonly Guid UpdaterGuid = new Guid("0D46C6D7-3058-4AE0-8BCE-0330A07B37DE");
        
        private IList<JoinRule> _rules;
        private readonly UpdaterId _updaterId;
        private static AutoJoinUpdater _instance;
        private static bool _isRegistered;
        private static bool _isProcessing = false;

        /// <summary>Biến static điều khiển việc có xử lý join hay không.</summary>
        public static bool IsEnabled { get; set; }

        private AutoJoinUpdater(AddInId addInId)
        {
            _updaterId = new UpdaterId(addInId, UpdaterGuid);
            _rules = JoinConfigService.Load();
            IsEnabled = JoinConfigService.LoadDmuEnabled();
        }

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>
        /// Đăng ký DMU Global. Gọi một lần duy nhất trong App.OnStartup.
        /// </summary>
        public static void Register(AddInId addInId)
        {
            if (_isRegistered) return;
            if (addInId == null) throw new ArgumentNullException(nameof(addInId));

            _instance = new AutoJoinUpdater(addInId);

            try
            {
                // Đăng ký updater với Revit (Global cho cả session)
                if (!UpdaterRegistry.IsUpdaterRegistered(_instance._updaterId))
                {
                    UpdaterRegistry.RegisterUpdater(_instance);
                }

                // Trigger cho 4 categories phổ biến
                var categories = new[]
                {
                    BuiltInCategory.OST_StructuralColumns,
                    BuiltInCategory.OST_StructuralFraming,
                    BuiltInCategory.OST_Walls,
                    BuiltInCategory.OST_Floors,
                    BuiltInCategory.OST_Toposolid,
                    BuiltInCategory.OST_StructuralFoundation
                };

                foreach (var cat in categories)
                {
                    var filter = new ElementCategoryFilter(cat);
                    
                    // Thêm trigger Application-wide (không chỉ cho 1 doc)
                    UpdaterRegistry.AddTrigger(_instance._updaterId, filter, Element.GetChangeTypeElementAddition());
                    UpdaterRegistry.AddTrigger(_instance._updaterId, filter, Element.GetChangeTypeGeometry());
                }

                _isRegistered = true;
            }
            catch (Exception ex)
            {
                // Log lỗi nếu cần, nhưng không Show dialog ở đây vì đang trong OnStartup
                Console.WriteLine($"[AutoJoin] Register DMU Error: {ex.Message}");
            }
        }

        /// <summary>Cập nhật rules khi user thay đổi config.</summary>
        public static void RefreshRules(IList<JoinRule> rules = null)
        {
            if (_instance != null)
            {
                _instance._rules = rules ?? JoinConfigService.Load();
            }
        }

        // ── IUpdater implementation ─────────────────────────────────────

        public void Execute(UpdaterData data)
        {
            if (_isProcessing) return;

            try
            {
                _isProcessing = true;

                // Nếu User tắt Realtime hoặc không có rules -> thoát ngay
                if (!IsEnabled || _rules == null || _rules.Count == 0) return;

                var doc = data.GetDocument();
                if (doc == null || !doc.IsValidObject) return;

                // Thu thập danh sách ID thay đổi
                var modifiedIds = data.GetModifiedElementIds();
                var addedIds = data.GetAddedElementIds();
                
                var allIds = addedIds.Concat(modifiedIds).Distinct();

                foreach (var id in allIds)
                {
                    try 
                    {
                        var element = doc.GetElement(id);
                        if (element == null || !element.IsValidObject || element.Category == null) continue;

                        // Bỏ qua các đối tượng Opening để tránh xung đột với AutoJoin-Link
                        if (element.Category.Id.Value == (long)BuiltInCategory.OST_SWallRectOpening)
                            continue;

                        AutoJoinService.JoinSingleElement(doc, element, _rules);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warning($"[AutoJoin DMU] Element {id} error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "[AutoJoin DMU] Execute error");

                // Tuyệt đối không để throw ra ngoài tránh Revit hiện dialog Error 1
            }
            finally
            {
                _isProcessing = false;
            }
        }

        public ChangePriority GetChangePriority() => ChangePriority.FloorsRoofsStructuralWalls;
        public UpdaterId GetUpdaterId() => _updaterId;
        public string GetUpdaterName() => "AutoJoin – Realtime Join Updater";
        public string GetAdditionalInformation() => "Tự động Join Geometry khi phần tử kết cấu được tạo hoặc chỉnh sửa.";
    }
}

