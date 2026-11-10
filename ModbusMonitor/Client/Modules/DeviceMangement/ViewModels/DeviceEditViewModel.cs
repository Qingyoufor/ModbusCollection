using Client.Infrastructure;
using Client.Models;
using Client.Models.Dtos;
using Client.Service;
using System.Collections.ObjectModel;
using System.Threading.Tasks;


namespace Client.Modules.DeviceManagement.ViewModels
{
    public class DeviceEditViewModel : BindableBase, IDialogAware
    {
        private readonly IDeviceService _deviceService;
        private readonly IDataPointService _dataPointService;

        /// <summary>编辑模式下被用户删除的已有测点 Id，保存时统一提交删除</summary>
        private readonly List<int> _deletedPointIds = new();

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
        public DelegateCommand AddPointCommand { get; }
        public DelegateCommand<DataPointEditModel> DeletePointCommand { get; }

        public ObservableCollection<DataPointEditModel> DataPoints { get; } = new();

        public IReadOnlyList<EnumOption<RegisterType>> RegisterTypeOptions { get; } = new List<EnumOption<RegisterType>>
        {
            new(RegisterType.HoldingRegister, "保持寄存器 (03)"),
            new(RegisterType.InputRegister, "输入寄存器 (04)")
        };

        public IReadOnlyList<EnumOption<DataPointType>> DataTypeOptions { get; } = new List<EnumOption<DataPointType>>
        {
            new(DataPointType.Int16, "Int16"),
            new(DataPointType.UInt16, "UInt16"),
            new(DataPointType.Int32, "Int32"),
            new(DataPointType.Float, "Float")
        };

        public DeviceEditViewModel(IDeviceService deviceService, IDataPointService dataPointService)
        {
            _deviceService = deviceService;
            _dataPointService = dataPointService;

            CancelCommand = new DelegateCommand(ExcuteCancel);
            SaveCommand = new AsyncDelegateCommand(ExcuteSave);
            AddPointCommand = new DelegateCommand(ExcuteAddPoint);
            DeletePointCommand = new DelegateCommand<DataPointEditModel>(ExcuteDeletePoint);
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

            var newPoints = DataPoints.Where(p => p.IsNew).ToList();
            if (newPoints.Any(p => string.IsNullOrWhiteSpace(p.Name)))
            {
                ErrorMessage = "测点名称不能为空";
                HasError = true;
                return;
            }

            int deviceId = _editId;
            if (_editId > 0)   // 有Id，编辑device模式
            {
                bool updated = await _deviceService.UpdateAsync(_editId, new UpdateDeviceRequest
                {
                    Name = Name,
                    Code = Code,
                    SlaveAddress = SlaveAddress,
                    ComPort = ComPort,
                    Description = Description
                });

                if (!updated)
                {
                    _hasError = true;
                    ErrorMessage = "保存失败，请检查数据或网络连接";
                    return;
                }
            }
            else     // 创建device模式
            {
                var created = await _deviceService.CreateAsync(new CreateDeviceRequest
                {
                    Name = Name,
                    Code = Code,
                    SlaveAddress = SlaveAddress,
                    ComPort = ComPort,
                    Description = Description
                });

                if (created == null)
                {
                    _hasError = true;
                    ErrorMessage = "保存失败，请检查数据或网络连接";
                    return;
                }

                deviceId = created.Id;
            }

            if (!await SaveDataPointsAsync(deviceId, newPoints))
            {
                return;
            }

            RequestClose.Invoke(new DialogResult(ButtonResult.OK));
        }

        /// <summary>先删除标记的测点，再逐条创建新增的测点；任一步失败就在弹窗里提示，不关闭界面</summary>
        private async Task<bool> SaveDataPointsAsync(int deviceId, List<DataPointEditModel> newPoints)
        {
            foreach (int pointId in _deletedPointIds)
            {
                if (!await _dataPointService.DeleteAsync(pointId))
                {
                    ErrorMessage = "设备已保存，但有测点删除失败，请重试";
                    HasError = true;
                    return false;
                }
            }
            _deletedPointIds.Clear();

            // 新测点的排序号接在已有测点之后，避免编辑模式下从 1 重排、与已有测点重复
            int baseSortOrder = DataPoints.Where(p => !p.IsNew).Select(p => p.SortOrder).DefaultIfEmpty(0).Max();

            for (int i = 0; i < newPoints.Count; i++)
            {
                if (!await _dataPointService.CreateAsync(deviceId, newPoints[i].ToCreateRequest(baseSortOrder + i + 1)))
                {
                    ErrorMessage = $"设备已保存，但测点「{newPoints[i].Name}」创建失败";
                    HasError = true;
                    return false;
                }
            }

            return true;
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

                    RaisePropertyChanged(nameof(DialogTitle));

                    var points = await _dataPointService.GetByDeviceIdAsync(device.Id);
                    if (points != null)
                    {
                        foreach (var point in points)
                        {
                            DataPoints.Add(DataPointEditModel.FromDto(point));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 异常可以在这里安全处理
                ErrorMessage = $"加载数据失败：{ex.Message}";
                HasError = true;
            }
        }

        private void ExcuteAddPoint()
        {
            DataPoints.Add(new DataPointEditModel { IsNew = true });
        }

        private void ExcuteDeletePoint(DataPointEditModel? point)
        {
            if (point == null) return;

            if (!point.IsNew)
            {
                _deletedPointIds.Add(point.Id);
            }
            DataPoints.Remove(point);
        }

        private void ExcuteCancel()
        {
            RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        #region 属性
        public string DialogTitle => _editId > 0 ? "编辑设备" : "新增设备";

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
