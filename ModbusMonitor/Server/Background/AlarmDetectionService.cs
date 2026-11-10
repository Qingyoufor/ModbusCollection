using Server.Infrastructure.ModbusRTU;
using Server.Infrastructure.Repositories;
using Server.Models.Common;
using Server.Models.Entities;

namespace Server.Background
{
    public class AlarmDetectionService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ModbusDataCache _cache;
        private readonly ILogger<AlarmDetectionService> _logger;

        public AlarmDetectionService(
            IServiceScopeFactory scopeFactory,
            ModbusDataCache cache,
            ILogger<AlarmDetectionService> logger)
        {
            _scopeFactory = scopeFactory;
            _cache = cache;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("报警检测引擎启动");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DetectAlarmsAsync();
                    await Task.Delay(1000, stoppingToken); // 每 1 秒检测一次
                }
                catch (OperationCanceledException)
                {
                    break;  // 停机信号，正常退出循环
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "报警检测异常");
                    // 退避等待不带 token：避免停机时在 catch 块内二次抛取消异常
                    await Task.Delay(5000, CancellationToken.None);
                }
            }
            _logger.LogInformation("报警检测引擎停止");
        }

        private async Task DetectAlarmsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var dataPointRepo = scope.ServiceProvider.GetRequiredService<IDataPointRepository>();
            var alarmRepo = scope.ServiceProvider.GetRequiredService<IAlarmRepository>();
            var allCacheData = _cache.GetAllData();

            // 一次全量加载测点，按设备分组（避免逐设备查库，每秒 N 条 SQL → 1 条）
            var pointsByDevice = (await dataPointRepo.GetAllAsync()).ToLookup(p => p.DeviceId);

            foreach (var (deviceId, pointData) in allCacheData)
            {
                foreach (var dataPoint in pointsByDevice[deviceId])
                {
                    if (!pointData.TryGetValue(dataPoint.Id, out double currentValue))
                        continue;

                    if (dataPoint.AlarmHigh.HasValue && currentValue > dataPoint.AlarmHigh)
                    {
                        // 检测高限报警
                        await CreateAlarmIfNotExistsAsync(
                            alarmRepo, deviceId, dataPoint, currentValue, AlarmType.HighLimit);
                    }

                    // 检测低限报警
                    if (dataPoint.AlarmLow.HasValue && currentValue < dataPoint.AlarmLow.Value)
                    {
                        await CreateAlarmIfNotExistsAsync(
                            alarmRepo, deviceId, dataPoint, currentValue, AlarmType.LowLimit);
                    }
                }
            }
        }

        /// <summary>
        /// 报警去重：同一测点、同一类型、未确认的报警不重复生成
        /// </summary>
        private async Task CreateAlarmIfNotExistsAsync(
            IAlarmRepository alarmRepo,
            int deviceId,
            DataPoint dataPoint,
            double currentValue,
            AlarmType alarmType)
        {
            // 阈值：高限报警取 AlarmHigh，低限报警取 AlarmLow
            var threshold = alarmType == AlarmType.HighLimit ? dataPoint.AlarmHigh!.Value : dataPoint.AlarmLow!.Value;

            // 查询是否存在未确认的同类报警
            var existAlarm = await alarmRepo.GetUnacknowledgedAlarmAsync(deviceId, dataPoint.Id, alarmType);
            if (existAlarm != null)
                return;  // 已存在未确认报警，不重复生成（去重）



            var alarm = new Alarm
            {
                DeviceId = deviceId,
                DataPointId = dataPoint.Id,

                AlarmType = alarmType,
                Message = $"{dataPoint.Name} {GetAlarmTypeText(alarmType)}报警（当前值: {currentValue:F2}, 阈值: {threshold:F2}）",
                Value = currentValue,
                Threshold = threshold,
                Status = AlarmStatus.Unacknowledged,
                OccurredAt = DateTime.UtcNow
            };

            await alarmRepo.AddAsync(alarm);
            await alarmRepo.SaveChangedAsync();

            _logger.LogWarning("报警生成: {Message}", alarm.Message);
        }

        private string GetAlarmTypeText(AlarmType type) => type == AlarmType.HighLimit ? "高限" : "低限";
    }
}
