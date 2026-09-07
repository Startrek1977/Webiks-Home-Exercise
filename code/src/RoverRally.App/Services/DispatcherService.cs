using System;
using System.Windows;

namespace RoverRally.App.Services
{
    public class DispatcherService : IDispatcherService
    {
        public void Invoke(Action action)
        {
            Application.Current.Dispatcher.BeginInvoke(action);
        }
    }
}
