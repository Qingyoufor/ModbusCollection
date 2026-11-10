using Server.Models.Common;
using Server.Models.Dtos;

namespace Server.Services
{
    public interface ISystemService
    {
        Task<ApiResult<SystemInfoDto>> GetInfoAsync();
        Task<ApiResult<List<SystemSettingsDto>>> GetSettingsAsync();
        Task<ApiResult> UpdateSettingsAsync(List<SystemSettingsDto> settings);
        Task<ApiResult> ReloadModbusAsync();
    }
}