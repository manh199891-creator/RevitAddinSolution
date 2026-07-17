using System;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Antigravity.HoanThien.Handlers
{
    public enum RequestId
    {
        None = 0,
        RunHoanThien = 1,
        CheckRooms = 2
    }

    public class HoanThienRequestHandler : IExternalEventHandler
    {
        private RequestId _request = RequestId.None;
        private UI.HoanThienViewModel _viewModel;
        private Window _window;

        public RequestId Request => _request;

        public void MakeRequest(RequestId request, UI.HoanThienViewModel viewModel, Window window)
        {
            _request = request;
            _viewModel = viewModel;
            _window = window;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                switch (_request)
                {
                    case RequestId.RunHoanThien:
                        RunHoanThienProcess(app);
                        break;
                    case RequestId.CheckRooms:
                        RunCheckRoomsProcess(app);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _request = RequestId.None;
            }
        }

        private void RunHoanThienProcess(UIApplication app)
        {
            var handler = new HoanThienHandler();
            var result = handler.Execute(app, _viewModel);
            if (result.Success || result.CreatedWalls.Count > 0 || result.CreatedFloors.Count > 0)
            {
                MessageBox.Show($"Created {result.CreatedWalls.Count} walls and {result.CreatedFloors.Count} floors.\nSkipped: {result.Skipped} existing items.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                _window?.Close(); // Close window after success
            }
            else
            {
                MessageBox.Show("Errors occurred or no valid rooms found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RunCheckRoomsProcess(UIApplication app)
        {
            Services.RoomCheckerService.CheckUnplacedRooms(app);
        }

        public string GetName()
        {
            return "HoanThien Request Handler";
        }
    }
}
