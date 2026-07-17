using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using Antigravity.Core.Services;

namespace Antigravity.WallMepClash.UI
{
    public class SimpleEventHandler : IExternalEventHandler
    {
        private readonly object _syncRoot = new object();
        private readonly Queue<Action> _actions = new Queue<Action>();

        public void Enqueue(Action action)
        {
            if (action == null)
                return;

            lock (_syncRoot)
            {
                _actions.Enqueue(action);
            }
        }

        public void Execute(UIApplication app)
        {
            while (true)
            {
                Action action;
                lock (_syncRoot)
                {
                    if (_actions.Count == 0)
                        return;

                    action = _actions.Dequeue();
                }

                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "[WallMepClash] ExternalEvent action failed");
                }
            }
        }

        public string GetName()
        {
            return "Antigravity Wall-MEP Clash External Event Handler";
        }
    }
}
