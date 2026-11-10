using Server.Models.Entities;

namespace Server.Infrastructure.Repositories
{
    public interface IDeviceDataRepository : IRepositoryBase<DeviceData>
    {
        Task BulkInsertAsync(List<DeviceData> entities);   // 批量插入历史数据（归档服务用）
        Task<int> CleanupOldDataAsync(DateTime cutoff);    // 删除截止时间前的过期数据，返回受影响行数
        Task<List<DeviceData>> GetHistoryAsync(int deviceId, int dataPointId, DateTime start, DateTime end); // 按设备+测点+时间范围查历史
    }
}
