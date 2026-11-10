using Client.Models.Dtos;

namespace Client.Service
{
    public interface ISystemService
    {
        Task<SystemInfoDto?> GetInfoAsync();
        Task<List<SystemSettingsDto>?> GetSettingsAsync();
        Task<bool> UpdateSettingsAsync(List<SystemSettingsDto> settings);
        Task<bool> ReloadModbusAsync();
    }
}