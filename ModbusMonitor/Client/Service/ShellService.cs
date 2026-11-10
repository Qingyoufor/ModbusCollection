using Client.Views;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Client.Service
{
    public class ShellService : IShellService
    {
        private readonly IContainerProvider _container;
        private readonly IRegionManager _regionManager;
        public ShellService(IContainerProvider container,IRegionManager regionManager)
        {
            _container = container;
            _regionManager = regionManager;
        }
        public void LoginToShell()
        {
            var shellView = _container.Resolve<ShellView>();
            Application.Current.MainWindow.Close();
            RegionManager.SetRegionManager(shellView, _regionManager);
            Application.Current.MainWindow = shellView;
            shellView.Show();
        }
    }
}
