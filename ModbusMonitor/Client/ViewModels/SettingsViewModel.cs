// Client/ViewModels/SettingsViewModel.cs
using Client.Models;
using Client.Models.Dtos;
using Client.Service;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Client.ViewModels
{
    /// <summary>端口映射行（SlaveCom=从站端口，MasterCom=主站端口）</summary>
    public class PortMappingItem : BindableBase
    {
        private string _slaveCom = "COM2";
        private string _masterCom = "COM1";
        public string SlaveCom { get => _slaveCom; set => SetProperty(ref _slaveCom, value); }
        public string MasterCom { get => _masterCom; set => SetProperty(ref _masterCom, value); }
    }

    public class SettingsViewModel : BindableBase, INavigationAware
    {
        private readonly ISystemService _systemService;

        // ===== Modbus 配置 =====
        public ObservableCollection<PortMappingItem> PortMappings { get; } = new();
        private string _baudRate = "9600";
        private string _dataBits = "8";
        private string _stopBits = "One";
        private string _parity = "None";
        private string _readTimeout = "500";
        private string _writeTimeout = "500";
        private string _pollIntervalMs = "1000";

        // ===== 数据保留 =====
        private string _deviceDataDays = "30";
        private string _alarmDays = "30";

        // ===== 日志 =====

        // ===== 关于 =====
        private SystemInfoDto _systemInfo = new();

        private bool _isLoading;
        private string _statusMessage = string.Empty;
        private bool _isStatusError;

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand ReloadModbusCommand { get; }
        public DelegateCommand AddMappingCommand { get; }
        public DelegateCommand<object?> RemoveMappingCommand { get; }
        public DelegateCommand LoadInfoCommand { get; }

        public SettingsViewModel(ISystemService systemService)
        {
            _systemService = systemService;

            SaveCommand = new DelegateCommand(async () => await SaveAsync(), () => !IsLoading);
            ReloadModbusCommand = new DelegateCommand(async () => await ReloadModbusAsync(), () => !IsLoading);
            AddMappingCommand = new DelegateCommand(() => PortMappings.Add(new PortMappingItem()));
            RemoveMappingCommand = new DelegateCommand<object?>(RemoveMapping);
            LoadInfoCommand = new DelegateCommand(async () => await LoadInfoAsync(), () => !IsLoading);
        }

        // ===== INavigationAware =====
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            await LoadAllAsync();
        }

        /// <summary>进入页面加载全部数据：配置 + 系统信息</summary>
        private async Task LoadAllAsync()
        {
            IsLoading = true;
            try
            {
                var settingsTask = _systemService.GetSettingsAsync();
                var infoTask = _systemService.GetInfoAsync();
                await Task.WhenAll(settingsTask, infoTask);

                var settings = await settingsTask;
                if (settings != null)
                    ApplySettings(settings);

                var info = await infoTask;
                if (info != null)
                    SystemInfo = info;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>把键值对配置映射到界面字段</summary>
        private void ApplySettings(List<SystemSettingsDto> settings)
        {
            var dict = settings.ToDictionary(s => s.Key, s => s.Value);

            PortMappings.Clear();
            var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                dict.GetValueOrDefault("Modbus.PortMappings", "{}"));
            if (mappings != null)
                foreach (var (slaveCom, masterCom) in mappings)
                    PortMappings.Add(new PortMappingItem { SlaveCom = slaveCom, MasterCom = masterCom });

            BaudRate = dict.GetValueOrDefault("Modbus.BaudRate", "9600");
            DataBits = dict.GetValueOrDefault("Modbus.DataBits", "8");
            StopBits = dict.GetValueOrDefault("Modbus.StopBits", "One");
            Parity = dict.GetValueOrDefault("Modbus.Parity", "None");
            ReadTimeout = dict.GetValueOrDefault("Modbus.ReadTimeout", "500");
            WriteTimeout = dict.GetValueOrDefault("Modbus.WriteTimeout", "500");
            PollIntervalMs = dict.GetValueOrDefault("Modbus.PollIntervalMs", "1000");
            DeviceDataDays = dict.GetValueOrDefault("DataRetention.DeviceDataDays", "30");
            AlarmDays = dict.GetValueOrDefault("DataRetention.AlarmDays", "30");
        }

        /// <summary>把界面字段汇总成配置清单并保存</summary>
        private async Task SaveAsync()
        {
            IsLoading = true;
            StatusMessage = string.Empty;
            IsStatusError = false;
            try
            {
                // 端口映射：从行列表反序列化为 {从站:主站} 字典 → JSON
                var mappings = PortMappings
                    .Where(m => !string.IsNullOrWhiteSpace(m.SlaveCom) && !string.IsNullOrWhiteSpace(m.MasterCom))
                    .ToDictionary(m => m.SlaveCom.Trim(), m => m.MasterCom.Trim());

                DeviceDataDays = NormalizeRetentionDays(DeviceDataDays);
                AlarmDays = NormalizeRetentionDays(AlarmDays);

                var settings = new List<SystemSettingsDto>
                {
                    new() { Key = "Modbus.PortMappings", Value = JsonSerializer.Serialize(mappings) },
                    new() { Key = "Modbus.BaudRate", Value = BaudRate },
                    new() { Key = "Modbus.DataBits", Value = DataBits },
                    new() { Key = "Modbus.StopBits", Value = StopBits },
                    new() { Key = "Modbus.Parity", Value = Parity },
                    new() { Key = "Modbus.ReadTimeout", Value = ReadTimeout },
                    new() { Key = "Modbus.WriteTimeout", Value = WriteTimeout },
                    new() { Key = "Modbus.PollIntervalMs", Value = PollIntervalMs },
                    new() { Key = "DataRetention.DeviceDataDays", Value = DeviceDataDays },
                    new() { Key = "DataRetention.AlarmDays", Value = AlarmDays }
                };

                var ok = await _systemService.UpdateSettingsAsync(settings);
                StatusMessage = ok ? "保存成功，Modbus 已重载" : "保存失败";
                IsStatusError = !ok;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ReloadModbusAsync()
        {
            var ok = await _systemService.ReloadModbusAsync();
            StatusMessage = ok ? "Modbus 已重载" : "重载失败";
            IsStatusError = !ok;
        }

        /// <summary>保留天数只接受正整数，空值或 0 按 1 天处理</summary>
        private static string NormalizeRetentionDays(string value) =>
            int.TryParse(value, out var days) && days >= 1 ? days.ToString() : "1";

        private void RemoveMapping(object? parameter)
        {
            if (parameter is PortMappingItem item)
                PortMappings.Remove(item);
        }

        private async Task LoadInfoAsync()
        {
            var info = await _systemService.GetInfoAsync();
            if (info != null)
                SystemInfo = info;
        }

        // ===== 属性（全部走 SetProperty 通知 UI）=====
        public string BaudRate { get => _baudRate; set => SetProperty(ref _baudRate, value); }
        public string DataBits { get => _dataBits; set => SetProperty(ref _dataBits, value); }
        public string StopBits { get => _stopBits; set => SetProperty(ref _stopBits, value); }
        public string Parity { get => _parity; set => SetProperty(ref _parity, value); }
        public string ReadTimeout { get => _readTimeout; set => SetProperty(ref _readTimeout, value); }
        public string WriteTimeout { get => _writeTimeout; set => SetProperty(ref _writeTimeout, value); }
        public string PollIntervalMs { get => _pollIntervalMs; set => SetProperty(ref _pollIntervalMs, value); }
        public string DeviceDataDays { get => _deviceDataDays; set => SetProperty(ref _deviceDataDays, value); }
        public string AlarmDays { get => _alarmDays; set => SetProperty(ref _alarmDays, value); }
        public SystemInfoDto SystemInfo { get => _systemInfo; set => SetProperty(ref _systemInfo, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsStatusError { get => _isStatusError; set => SetProperty(ref _isStatusError, value); }
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    SaveCommand.RaiseCanExecuteChanged();
                    ReloadModbusCommand.RaiseCanExecuteChanged();
                    LoadInfoCommand.RaiseCanExecuteChanged();
                }
            }
        }
    }
}