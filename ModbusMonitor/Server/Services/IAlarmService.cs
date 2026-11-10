using Server.Models.Common;
using Server.Models.Dtos;

namespace Server.Services
{
    public interface IAlarmService
    {
        Task<ApiResult<List<AlarmDto>>> GetCurrentAlarmsAsync();
        Task<ApiResult<PagedResult<AlarmDto>>> GetHistoryAsync(int page, int pageSize, int? deviceId, string? status);
        Task<ApiResult> AcknowledgeAsync(long alarmId, string acknowledgedBy);
    }
}
