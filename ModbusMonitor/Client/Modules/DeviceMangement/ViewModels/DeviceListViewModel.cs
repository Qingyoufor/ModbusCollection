using Client.Models.Dtos;
using Client.Modules.DeviceMangement.Views;
using Client.Service;
using System.Collections.ObjectModel;
using System.Windows;


namespace Client.Modules.DeviceManagement.ViewModels
{
    public class DeviceListViewModel : BindableBase
    {
		private readonly IDeviceService _deviceService;
		private readonly IDialogService _dialogService;

		private List<DeviceListItemDto>? _devices;
		public string PageInfo => $"{pageIndex} / {maxPage}";

		private int pageIndex = 1;
		private int pageSize = 2;
		private int totalCount;
		private int maxPage => (int)Math.Ceiling((double) totalCount / pageSize); 

		public AsyncDelegateCommand RefreshCommand { get; }
		public AsyncDelegateCommand CreateCommand { get; }

		// 必须设为<int?>,xaml的数据可能没有加载完毕，绑定的Device.Id可能为null，导致抛出难以定位的异常
        public AsyncDelegateCommand<int?> EditCommand { get; }
		public AsyncDelegateCommand<int?> DeleteCommand { get; }
		public AsyncDelegateCommand PrevPageCommand { get; }
		public AsyncDelegateCommand NextPageCommand { get; }


        public DeviceListViewModel(IDeviceService deviceService,IDialogService dialogService)
        {
			_deviceService = deviceService;
			_dialogService = dialogService;

			RefreshCommand = new AsyncDelegateCommand(LoadData);
			CreateCommand = new AsyncDelegateCommand(ExecuteCreate);
			EditCommand = new AsyncDelegateCommand<int?>(ExcuteEdit);
			DeleteCommand = new AsyncDelegateCommand<int?>(ExcuteDelete);
			PrevPageCommand = new AsyncDelegateCommand(ExcutePrevPage, CanPrev);
			NextPageCommand = new AsyncDelegateCommand(ExcuteNextPage, CanNext);

			_ = LoadData();
        }

        #region 分页
        private bool CanPrev() => pageIndex > 1;
		private bool CanNext() => pageIndex < maxPage;

		private async Task ExcutePrevPage()
		{
			if (CanPrev())
			{
				pageIndex--;
				await LoadData();
			}
		}
		private async Task ExcuteNextPage()
		{
			if (CanNext())
			{
				pageIndex++;
				await LoadData();
			}
		}

        #endregion

		// 加载页面数据
        private async Task LoadData()
		{
			var devicePage = await _deviceService.GetPagedAsync(pageIndex,pageSize);
			if(devicePage != null)
			{
				// 刷新属性、按钮状态
				Devices = devicePage.Items;
				totalCount = devicePage.TotalCount;
				RaisePropertyChanged(nameof(PageInfo));
				RaisePropertyChanged(nameof(maxPage));
				PrevPageCommand.RaiseCanExecuteChanged();
				NextPageCommand.RaiseCanExecuteChanged();
			}
		}

		// 新建Device
        private async Task ExecuteCreate()
        {
            var parameters = new DialogParameters(); // 空参数包，用于空列表创建Device
            _dialogService.ShowDialog("DeviceEditView", parameters, async result =>
            {
                if (result.Result == ButtonResult.OK)
                    await LoadData();
            });
        }

		// 编辑Device 通过xaml绑定的Id找到device
		private async Task ExcuteEdit(int? id)
		{
			if(id == null) return;
			var device = await _deviceService.GetByIdAsync((int)id);
			if(device != null)
			{
                var paraeters = new DialogParameters();
				paraeters.Add("device", device);
				_dialogService.ShowDialog(nameof(DeviceEditView), paraeters, async result =>
				{
					if (result.Result == ButtonResult.OK)
						await LoadData();
				});
            }
		}

		// 删除device
		private async Task ExcuteDelete(int? id)
		{
			if(id == null) return;
			var isDelete = MessageBox.Show("确定要删除该设备吗？此操作不可撤销", "确认删除", MessageBoxButton.OKCancel);
			if (isDelete == MessageBoxResult.OK)
			{
				var success = await _deviceService.DeleteAsync((int)id);
				if (success)
					await LoadData();
			}
		}

		// ========属性========
		public List<DeviceListItemDto>? Devices
		{
			get => _devices;
			set => SetProperty(ref _devices, value);
		}
    }
}