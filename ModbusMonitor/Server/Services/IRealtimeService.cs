using Server.Models.Common;
using Server.Models.Dtos;

namespace Server.Services
{
    public interface IRealtimeService
    {
        Task<ApiResult<RealtimeAllDataDto>> GetAllDataAsync();
        Task<ApiResult<RealtimeDeviceDataDto>> GetDeviceDataAsync(int deviceId);
    }
}
