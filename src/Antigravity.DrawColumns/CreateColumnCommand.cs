using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using Antigravity.Core.Services;
using Antigravity.Core.UI;

namespace Antigravity.DrawColumns
{
    [Transaction(TransactionMode.Manual)]
    public class CreateColumnCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var revitApp = commandData.Application.Application;

                // === KIỂM TRA BẢO MẬT ===
                bool needPassword;
                bool authorized = SecurityService.IsAuthorized(revitApp, out needPassword);

                if (!authorized)
                {
                    if (needPassword)
                    {
                        // Hiện hộp thoại nhập mật khẩu
                        PasswordWindow pwWindow = new PasswordWindow();
                        pwWindow.ShowDialog();

                        if (!pwWindow.IsUnlocked)
                        {
                            // Người dùng hủy hoặc nhập sai
                            TaskDialog.Show("Truy cập bị từ chối",
                                "Bạn không có quyền sử dụng Add-in này.\nVui lòng liên hệ VilaiViet để được cấp quyền.");
                            return Result.Cancelled;
                        }
                    }
                    else
                    {
                        TaskDialog.Show("Truy cập bị từ chối",
                            "Bạn không có quyền sử dụng Add-in này.\nVui lòng liên hệ VilaiViet để được cấp quyền.");
                        return Result.Cancelled;
                    }
                }
                // === KẾT THÚC KIỂM TRA BẢO MẬT ===

                UI.MainWindow window = new UI.MainWindow(commandData.Application);
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
