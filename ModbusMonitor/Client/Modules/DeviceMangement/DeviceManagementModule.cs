
using Client.Modules.DeviceManagement.ViewModels;
using Client.Modules.DeviceMangement.Views;


namespace Client.Modules.DeviceMangement
{
    public class DeviceManagementModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<DeviceListView, DeviceListViewModel>();
            containerRegistry.RegisterDialog<DeviceEditView, DeviceEditViewModel>();// 必须实现IDialogAware
        }
    }
}
