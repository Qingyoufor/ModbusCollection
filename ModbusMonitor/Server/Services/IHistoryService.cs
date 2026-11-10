using Server.Models.Common;
using Server.Models.Dtos;

namespace Server.Services
{
    public interface IHistoryService
    {
        Task<ApiResult<List<HistoryDataPointDto>>> GetHistoryAsync(
            int deviceId, int dataPointId, DateTime start, DateTime end);

        Task<ApiResult<HistoryStatisticsDto>> GetStatisticsAsync(
            int deviceId, int dataPointId, DateTime start, DateTime end);
    }
}
