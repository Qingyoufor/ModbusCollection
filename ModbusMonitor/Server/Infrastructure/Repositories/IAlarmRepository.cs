using Server.Models.Common;
using Server.Models.Entities;

namespace Server.Infrastructure.Repositories
{
    public interface IAlarmRepository : IRepositoryBase<Alarm>
    {
        /// <summary>查询指定设备、测点、类型的未确认报警</summary>
        Task<Alarm?> GetUnacknowledgedAlarmAsync(int deviceId, int dataPointId, AlarmType alarmType);

        /// <summary>查询当前活跃报警（未确认）</summary>
        Task<List<Alarm>> GetCurrentAlarmsAsync();

        /// <summary>查询报警历史（分页 + 筛选）</summary>
        Task<PagedResult<Alarm>> GetHistoryAsync(int page, int pageSize, int? deviceId, AlarmStatus? status);

        /// <summary>确认报警</summary>
        Task<bool> AcknowledgeAsync(long alarmId, string acknowledgedBy);

        Task<int> CleanupOldAlarmsAsync(DateTime cutoff);
    }
}
