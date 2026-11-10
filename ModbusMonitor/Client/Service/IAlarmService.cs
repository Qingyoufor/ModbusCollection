using Client.Models.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Service
{
    public interface IAlarmService
    {
        Task<PagedResult<AlarmDto>?> GetHistoryAsync(int page, int pageSize, int? deviceId, string? status);
        Task<bool> AcknowledgeAsync(long alarmId);
    }
}
