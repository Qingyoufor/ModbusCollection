using Server.Infrastructure.ModbusRTU;
using Server.Models.Common;
using Server.Models.Dtos;
using System.Diagnostics;

namespace Server.Services
{
    public class SystemService : ISystemService
    {
        private readonly IModbusService _modbusService;
        private readonly ModbusDataCache _cache;
        private readonly ISystemConfigService _configService;
        private readonly ILogger<SystemService> _logger;

        public SystemService(
            IModbusService modbusService,
            ModbusDataCache cache,
            ISystemConfigService configService,
            ILogger<SystemService> logger)
        {
            _modbusService = modbusService;
            _cache = cache;
            _configService = configService;
            _logger = logger;
        }

        public async Task<ApiResult<SystemInfoDto>> GetInfoAsync()
        {
            var allData = _cache.GetAllData();

            var elapsed = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();

            var info = new SystemInfoDto
            {
                Version = typeof(SystemService).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                ServerStartedAt = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                UpTime = $"{(int)elapsed.TotalDays}天 {elapsed.Hours}小时 {elapsed.Minutes}分钟",
                OnlineDeviceCount = allData.Count,                       // 有缓存数据的设备视为在线
                CachedPointCount = allData.Values.Sum(d => d.Count),     // 缓存测点总数
                ModbusRunning = _modbusService.IsRunning,
                LastPollTime = _modbusService.LastPollTime
            };

            _logger.LogInformation("查询系统信息：在线设备 {Devices}，测点 {Points}", info.OnlineDeviceCount, info.CachedPointCount);
            return ApiResult<SystemInfoDto>.Success(info);
        }

        public async Task<ApiResult<List<SystemSettingsDto>>> GetSettingsAsync()
        {
            // 直接按已知键清单从内存缓存读取（单例缓存，无需访问文件）
            var keys = new[]
            {
                "Modbus.PortMappings", "Modbus.BaudRate", "Modbus.DataBits", "Modbus.StopBits",
                "Modbus.Parity", "Modbus.ReadTimeout", "Modbus.WriteTimeout",
                "Modbus.PollIntervalMs", "DataRetention.DeviceDataDays", "DataRetention.AlarmDays"
            };

            var settings = keys.Select(key => new SystemSettingsDto
            {
                Key = key,
                Value = _configService.GetValueStr(key) ?? string.Empty
            }).ToList();

            return ApiResult<List<SystemSettingsDto>>.Success(settings);
        }

        public async Task<ApiResult> UpdateSettingsAsync(List<SystemSettingsDto> settings)
        {
            await _configService.AddOrUpdateAllAsync(settings);

            // 配置已进内存缓存，重载 Modbus（重建串口连接 + 重新加载设备配置）
            await _modbusService.ReloadConfigsAsync();

            _logger.LogInformation("系统配置已更新并触发 Modbus 重载，共 {Count} 项", settings.Count);
            return ApiResult.Success("配置保存成功");
        }

        public async Task<ApiResult> ReloadModbusAsync()
        {
            _logger.LogInformation("手动触发 Modbus 配置重载");
            await _modbusService.ReloadConfigsAsync();
            return ApiResult.Success("Modbus 已重载");
        }

    }
}