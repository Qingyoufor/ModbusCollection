using Microsoft.EntityFrameworkCore;
using Server.Infrastructure.Data;
using Server.Models.Common;
using Server.Models.Entities;

namespace Server.Infrastructure.Repositories
{
    public class AlarmRepository :RepositoryBase<Alarm>,IAlarmRepository
    {
        public AlarmRepository(ModbusMonitorDbContext context) : base(context) { }

        public async Task<Alarm?> GetUnacknowledgedAlarmAsync(int deviceId, int dataPointId, AlarmType alarmType)
        {
            return await _dbSet.Where(a => a.DeviceId == deviceId
                                    && a.DataPointId == dataPointId
                                    && a.AlarmType == alarmType
                                    && a.Status == AlarmStatus.Unacknowledged)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Alarm>> GetCurrentAlarmsAsync()
        {
            return await _dbSet
                .Where(a => a.Status == AlarmStatus.Unacknowledged)
                .Include(a => a.Device)
                .Include(a => a.DataPoint)
                .OrderByDescending(a => a.OccurredAt)
                .ToListAsync();  
        }

        public async Task<PagedResult<Alarm>> GetHistoryAsync(int page, int pageSize, int? deviceId, AlarmStatus? status)
        {
            var query = _dbSet
                .Include(a => a.Device)
                .Include(a => a.DataPoint)
                .AsQueryable();

            if (deviceId.HasValue)
                query = query.Where(a => a.DeviceId == deviceId);


            if (status.HasValue)
                query = query.Where(a => a.Status == status.Value);

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.OccurredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Alarm>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<bool> AcknowledgeAsync(long alarmId, string acknowledgedBy)
        {
            var alarm = await _dbSet.FindAsync(alarmId);
            if (alarm == null || alarm.Status == AlarmStatus.Acknowledged)
                return false;

            alarm.Status = AlarmStatus.Acknowledged;
            alarm.AcknowledgedBy = acknowledgedBy;
            alarm.AcknowledgedAt = DateTime.UtcNow;

            // FindAsync 已跟踪实体，直接改属性即可，无需 Update
            await SaveChangedAsync();
            return true;
        }

        public async Task<int> CleanupOldAlarmsAsync(DateTime cutoff)
            // ExecuteDeleteAsync：一条 DELETE SQL 直接删除，不加载实体进内存
            => await _dbSet.Where(a => a.OccurredAt < cutoff).ExecuteDeleteAsync();
    }
}
