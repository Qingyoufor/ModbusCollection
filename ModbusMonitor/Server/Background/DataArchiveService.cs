using Server.Infrastructure.ModbusRTU;
using Server.Infrastructure.Repositories;
using Server.Models.Common;
using Server.Models.Entities;
using Server.Services;

namespace Server.Background
{
    public class DataArchiveService(
        IServiceScopeFactory _scopeFactory,
        ModbusDataCache _cache,
        ISystemConfigService _systemConfigService,
        ILogger<DataArchiveService> _logger
        ) : BackgroundService
    {
        // 归档间隔写死；保留天数走 CleanupAsync 现读配置
        private readonly TimeSpan _archiveInterval = TimeSpan.FromSeconds(5);
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("历史数据归档服务启动");
            await _systemConfigService.LoadAsync();   // 启动时加载配置
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ArchiveAsync();
                    await CleanupAsync();
                    await Task.Delay(_archiveInterval,stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // 正常停机，不记错误日志
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "历史数据归档异常");
                    // 退避等待不带 token：避免停机时在 catch 块内二次抛取消异常
                    await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);
                }
            }
            _logger.LogInformation("历史数据归档服务停止");
        }

        private async Task ArchiveAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var deviceDataRepo = scope.ServiceProvider.GetRequiredService<IDeviceDataRepository>();

            var allCacheData = _cache.GetAllData();
            var batch = new List<DeviceData>();
            var now = DateTime.UtcNow;

            // 能进缓存 = ConfigLoader 已按在线过滤 = 采集正在采的设备，无需再查 Device 表逐个核对状态
            foreach (var (deviceId, pointValues) in allCacheData)
            {
                foreach (var (pointId, value) in pointValues)
                {
                    batch.Add(new DeviceData
                    {
                        DeviceId = deviceId,
                        DataPointId = pointId,
                        Value = value,
                        RecordedAt = now,
                        CreatedAt = now
                    });
                }
            }

            if(batch.Count > 0)
            {
                await deviceDataRepo.BulkInsertAsync(batch);
                _logger.LogInformation("归档 {Count} 条历史数据", batch.Count);
            }
        }

        private async Task CleanupAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var deviceDataRepo = scope.ServiceProvider.GetRequiredService<IDeviceDataRepository>();
            var alarmRepo = scope.ServiceProvider.GetRequiredService<IAlarmRepository>();

            // 保留天数每次清理时现读，运行时改配置立即生效
            var deviceDataDays = _systemConfigService.GetValueInt("DataRetention.DeviceDataDays", 30);
            var alarmDays = _systemConfigService.GetValueInt("DataRetention.AlarmDays", 30);

            var removedData = await deviceDataRepo.CleanupOldDataAsync(DateTime.UtcNow.AddDays(-deviceDataDays));
            if (removedData > 0)
                _logger.LogInformation("清理 {Count} 条超过 {Days} 天的历史数据", removedData, deviceDataDays);

            var removedAlarms = await alarmRepo.CleanupOldAlarmsAsync(DateTime.UtcNow.AddDays(-alarmDays));
            if (removedAlarms > 0)
                _logger.LogInformation("清理 {Count} 条超过 {Days} 天的报警记录", removedAlarms, alarmDays);
        }
    }
}
