using Client.Infrastructure;
using Client.Models.Dtos;
using Client.Service;
using System.Collections.ObjectModel;
using System.Threading.Tasks;


namespace Client.Modules.DeviceManagement.ViewModels
{
    public class DeviceEditViewModel : BindableBase, IDialogAware
    {
        private readonly IDeviceService _deviceService;

        private string _name = string.Empty;
        private string _code = string.Empty;
        private byte _slaveAddress;
        private string _comPort = "COM2";
        private string? _dscription;
        private string _errorMessage = string.Empty;

        private bool _hasError;
        private int _editId;

        public AsyncDelegateCommand SaveCommand { get; }
        public DelegateCommand CancelCommand { get;}

        public DeviceEditViewModel(IDeviceService deviceService)
        {
            _deviceService = deviceService;

            CancelCommand = new DelegateCommand(ExcuteCancel);
            SaveCommand = new AsyncDelegateCommand(ExcuteSave);
        }

        // ====== IDialogAware 实现 ========
        public DialogCloseListener RequestClose { get; }  // RequestClose.Invoke( 回调 )关闭弹窗，传回调

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
           _ = LoadAsync(parameters);
        }

        // =====命令方法实现========
        private async Task ExcuteSave()
        {
            _hasError = false; // 重置错误
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Code))
            {
                ErrorMessage = "设备名称和编码不能为空";
                HasError = true;
                return;
            }

            bool success;
            if (_editId > 0)   // 有Id，编辑device模式
            {
                success = await _deviceService.UpdateAsync(_editId, new UpdateDeviceRequest
                {
                    Name = Name,
                    Code = Code,
                    SlaveAddress = SlaveAddress,
                    ComPort = ComPort,
                    Description = Description
                });
            }
            else     // 创建device模式
            {
                success = await _deviceService.CreateAsync(new CreateDeviceRequest
                {
                    Name = Name,
                    Code = Code,
                    SlaveAddress = SlaveAddress,
                    ComPort = ComPort,
                    Description = Description
                });
            }

            if (success)
            {
                RequestClose.Invoke(new DialogResult(ButtonResult.OK));  
            }
            else
            {
                _hasError = true;
                ErrorMessage = "保存失败，请检查数据或网络连接";
            }

        }

        // 自定义加载方法,避免 async void OnDialogOpened，出现异常时不抛出异常
        private async Task LoadAsync(IDialogParameters parameters)
        {
            try
            {
                // 编辑模式：回填数据
                if (parameters.TryGetValue("device", out DeviceDto device) && device != null)
                {
                    _editId = device.Id;
                    Name = device.Name;
                    Code = device.Code;
                    SlaveAddress = device.SlaveAddress;
                    ComPort = device.ComPort;
                    Description = device.Description;
                }
            }
            catch (Exception ex)
            {
                // 异常可以在这里安全处理
                ErrorMessage = $"加载数据失败：{ex.Message}";
                HasError = true;
            }
        }

        private void ExcuteCancel()
        {
            RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        #region 属性
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        public string Code
        {
            get => _code;
            set => SetProperty(ref _code, value);
        }
        public byte SlaveAddress
        {
            get => _slaveAddress;
            set => SetProperty(ref _slaveAddress, value);
        }
        public string ComPort
        {
            get => _comPort;
            set => SetProperty(ref _comPort, value);
        }


        public string? Description
        {
            get => _dscription;
            set => SetProperty(ref _dscription, value);
        }
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }
        #endregion
    }
}