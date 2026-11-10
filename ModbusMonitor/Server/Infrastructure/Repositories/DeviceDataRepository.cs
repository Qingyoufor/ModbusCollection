using Microsoft.EntityFrameworkCore;
using Server.Infrastructure.Data;
using Server.Models.Entities;

namespace Server.Infrastructure.Repositories
{
    public class DeviceDataRepository : RepositoryBase<DeviceData>, IDeviceDataRepository
    {
        public DeviceDataRepository(ModbusMonitorDbContext context) : base(context) { }

        public async Task BulkInsertAsync(List<DeviceData> entities)
        {
            await _dbSet.AddRangeAsync(entities);
            await SaveChangedAsync();
        }

        public async Task<int> CleanupOldDataAsync(DateTime cutoff)
            // ExecuteDeleteAsync：一条 DELETE SQL 直接删除，不加载实体进内存（最大表 + 每5秒调用，必须走数据库端）
            => await _dbSet.Where(d => d.RecordedAt < cutoff).ExecuteDeleteAsync();

        public Task<List<DeviceData>> GetHistoryAsync(int deviceId, int dataPointId, DateTime start, DateTime end)
        {
            // 只读大结果集：AsNoTracking 跳过变更跟踪；左闭右开区间 [start, end)
            return _dbSet
                .AsNoTracking()
                .Where(d => d.DeviceId == deviceId
                                && d.DataPointId == dataPointId
                                && d.RecordedAt >= start
                                && d.RecordedAt < end)
                .OrderBy(d => d.RecordedAt)
                .ToListAsync();
        }
    }
}
