using Client.Infrastructure;
using Client.Modules.DeviceMangement;
using Client.Service;
using Client.ViewModels;
using Client.Views;

using System.Windows;


namespace Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication
    {
        // Prism 仅对 CreateShell() 方法返回的顶级视图自动设置 RegionManager，
        // 对于通过 DialogService、手动 New 等方式创建的视图，需要手动关联。
        // 必须显式调用 RegionManager.SetRegionManager(视图实例, regionManager) 进行绑定。
        protected override Window CreateShell()
        {
            return Container.Resolve<LoginView>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<TokenStore>(); 
            containerRegistry.RegisterSingleton<HttpClientBase>(); 

            containerRegistry.RegisterSingleton<IAuthService,AuthService>();
            containerRegistry.RegisterSingleton<IShellService,ShellService>();
            containerRegistry.RegisterSingleton<IDeviceService,DeviceService>();
            containerRegistry.RegisterSingleton<IDataPointService, DataPointService>();
            containerRegistry.RegisterSingleton<IRealtimeService,RealtimeService>();
            containerRegistry.RegisterSingleton<IAlarmService, AlarmService>();
            containerRegistry.RegisterSingleton<IHistoryService, HistoryService>();
            containerRegistry.RegisterSingleton<ISystemService, SystemService>();

            containerRegistry.RegisterForNavigation<LoginView, LoginViewModel>();
            containerRegistry.RegisterForNavigation<RealtimeView, RealtimeViewModel>();
            containerRegistry.RegisterForNavigation<AlarmView, AlarmViewModel>();
            containerRegistry.RegisterForNavigation<SettingsView, SettingsViewModel>();
            containerRegistry.RegisterForNavigation<HistoryView, HistoryViewModel>();
        }

        // IModule
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            base.ConfigureModuleCatalog(moduleCatalog);
            moduleCatalog.AddModule<DeviceManagementModule>();
        }

    }

}
