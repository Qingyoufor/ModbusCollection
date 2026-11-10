using Client.Models;
using System.Windows;

namespace Client.ViewModels
{
    public class ShellViewModel : BindableBase
    {
        private readonly IRegionManager _regionManager;

        // 选中元素跳转到viewName对应的视图
        private MenuItemModel? _selectedMenuItem;
        public List<MenuItemModel> MenuItems { get; }

        public ShellViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;

            MenuItems = new List<MenuItemModel>
            {
                new MenuItemModel { Header = "实时监控", ViewName = "RealtimeView" },
                new MenuItemModel { Header = "设备管理", ViewName = "DeviceListView" }, // Module里的DeviceListView
                new MenuItemModel { Header = "报警中心", ViewName = "AlarmView" },
                new MenuItemModel { Header = "历史趋势", ViewName = "HistoryView" },
                new MenuItemModel { Header = "系统设置", ViewName = "SettingsView" }
            };

            // 延迟到视图加载完成后再设默认选中，而非构造函数里直接赋值：
            // 1) 构造函数时机 MainRegion 尚未注册，立即 RequestNavigate 会失败导致首屏空白；
            // 2) ListBox.SelectedItem 默认 TwoWay，初始化时可能把 null 写回覆盖此处的默认值。
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SelectedMenuItem = MenuItems[0];
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public MenuItemModel? SelectedMenuItem
        {
            get => _selectedMenuItem;
            set
            {
                if (SetProperty(ref _selectedMenuItem,value) && value != null)
                {
                    _regionManager.RequestNavigate("MainRegion", value.ViewName);
                }
            }
        }
    }
}
