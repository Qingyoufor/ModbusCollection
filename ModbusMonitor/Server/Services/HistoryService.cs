using Server.Infrastructure.Repositories;
using Server.Models.Common;
using Server.Models.Dtos;
using Server.Models.Entities;

namespace Server.Services
{
    public class HistoryService(
        IDeviceRepository _deviceRepo,
        IDataPointRepository _dataPointRepo,
        IDeviceDataRepository _deviceDataRepo) : IHistoryService
    {
        public async Task<ApiResult<List<HistoryDataPointDto>>> GetHistoryAsync(int deviceId, int dataPointId, DateTime start, DateTime end)
        {
            var device = await _deviceRepo.GetByIdAsync(deviceId);
            if (device == null)
                return ApiResult<List<HistoryDataPointDto>>.Fail(404, "设备不存在");

            var point = await _dataPointRepo.GetByIdAsync(dataPointId);
            if (point == null)
                return ApiResult<List<HistoryDataPointDto>>.Fail(404, "测点不存在");

            var raw = await _deviceDataRepo.GetHistoryAsync(deviceId, dataPointId, start, end);

            var dtos = raw.Select(d => new HistoryDataPointDto
            {
                DeviceId = deviceId,
                DeviceName = device.Name,
                DataPointId = dataPointId,
                DataPointName = point.Name,
                RecordedAt = d.RecordedAt,
                Value = d.Value
            }).ToList();

            return ApiResult<List<HistoryDataPointDto>>.Success(dtos);
        }

        public async Task<ApiResult<HistoryStatisticsDto>> GetStatisticsAsync(int deviceId, int dataPointId, DateTime start, DateTime end)
        {
            var raw = await _deviceDataRepo.GetHistoryAsync(deviceId,dataPointId, start, end);
            if(raw.Count == 0)
            {
                return ApiResult<HistoryStatisticsDto>.Fail(404, "该时间段无历史数据");
            }
            var device = await _deviceRepo.GetByIdAsync(deviceId);
            var point = await _dataPointRepo.GetByIdAsync(dataPointId);

            return ApiResult<HistoryStatisticsDto>.Success(new HistoryStatisticsDto
            {
                DeviceId = deviceId,
                DeviceName = device?.Name ?? string.Empty,
                DataPointId = dataPointId,
                DataPointName = point?.Name ?? string.Empty,
                Max = raw.Max(d => d.Value),
                Min = raw.Min(d => d.Value),
                Average = raw.Average(d => d.Value),
                Count = raw.Count
            });
        }

    }
}
