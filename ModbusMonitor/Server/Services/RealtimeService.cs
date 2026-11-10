using Server.Infrastructure.ModbusRTU;
using Server.Infrastructure.Repositories;
using Server.Models.Common;
using Server.Models.Dtos;
using Server.Models.Entities;

namespace Server.Services
{
    public class RealtimeService :IRealtimeService
    {
        private readonly ModbusDataCache _cache;
        private readonly IDeviceRepository _deviceRepo;
        private readonly IDataPointRepository _dataPointRepo;

        public RealtimeService(ModbusDataCache cache,IDeviceRepository deviceRepo,IDataPointRepository dataPointRepo)
        {
            _cache = cache;
            _deviceRepo = deviceRepo;
            _dataPointRepo = dataPointRepo;
        }

        public async Task<ApiResult<RealtimeAllDataDto>> GetAllDataAsync()
        {
            var devices = await _deviceRepo.GetAllAsync();
            var allCachedData = _cache.GetAllData(); // 所有设备的缓存数据（最新值）
            // 一次取回全部测点，按设备分组：避免每台设备一次查询（N+1）
            var pointsByDevice = (await _dataPointRepo.GetAllAsync()).ToLookup(p => p.DeviceId);
            var result = new RealtimeAllDataDto();

            foreach (var device in devices)
            {
                var dataPoints = pointsByDevice[device.Id];
                var cachedData = allCachedData.GetValueOrDefault(device.Id);
                var deviceDto = new RealtimeDeviceDataDto
                {
                    DeviceId = device.Id,
                    DeviceName = device.Name,
                    ComPort = device.ComPort
                };
                
                foreach (var point in dataPoints)
                {
                    //  将缓存值与数据库测点元信息关联
                    var value = cachedData?.GetValueOrDefault(point.Id) ?? 0;
                    deviceDto.DataPoints.Add(new DataPointValueDto
                    {
                        DataPointId = point.Id,
                        Name = point.Name,
                        Value = value,
                        Unit = point.Unit,
                        DataType = point.DataType.ToString(),
                        AlarmHigh = point.AlarmHigh,
                        AlarmLow = point.AlarmLow,
                        DecimalPlaces = point.DecimalPlaces,
                        AlarmStatus = DetermineAlarmStatus(point, value)
                    });
                }

                result.Devices.Add(deviceDto);
            }

            return ApiResult<RealtimeAllDataDto>.Success(result);
        }

        public async Task<ApiResult<RealtimeDeviceDataDto>> GetDeviceDataAsync(int deviceId)
        {
            var device = await _deviceRepo.GetByIdAsync(deviceId);
            if (device == null)
                return ApiResult<RealtimeDeviceDataDto>.Fail(404, "设备不存在");

            var deviceDto = new RealtimeDeviceDataDto
            {
                DeviceId = device.Id,
                DeviceName = device.Name
            };

            var points = await _dataPointRepo.GetByDeviceIdAsync(deviceId);
            var cachedDate = _cache.GetDeviceData(device.Id);

            foreach (var point in points)
            {
                var value = cachedDate?.GetValueOrDefault(point.Id) ?? 0;
                var pointDto = new DataPointValueDto
                {
                    DataPointId = point.Id,
                    Name = point.Name,
                    Value = value,
                    Unit = point.Unit,
                    DataType = point.DataType.ToString(),
                    AlarmHigh = point.AlarmHigh,
                    AlarmLow = point.AlarmLow,
                    DecimalPlaces = point.DecimalPlaces,
                    AlarmStatus = DetermineAlarmStatus(point, value)
                };
                deviceDto.DataPoints.Add(pointDto);
            }

            return ApiResult<RealtimeDeviceDataDto>.Success(deviceDto);
        }

        private static string DetermineAlarmStatus(DataPoint point, double value)
            => point.IsHighLimit(value) ? "High"
             : point.IsLowLimit(value) ? "Low"
             : "Normal";
    }
}
