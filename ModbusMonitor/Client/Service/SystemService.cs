using Client.Infrastructure;
using Client.Models.Dtos;
using System.Net.Http.Json;
using System.Text.Json;

namespace Client.Service
{
    public class SystemService(HttpClientBase _http) : ISystemService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public async Task<SystemInfoDto?> GetInfoAsync()
        {
            var response = await _http.GetAsync("api/system/info");
            if (!response.IsSuccessStatusCode)
                return null;
            var result = await response.Content.ReadFromJsonAsync<ApiResult<SystemInfoDto>>(_jsonOptions);
            return result?.Code == 200 ? result.Data : null;
        }

        public async Task<List<SystemSettingsDto>?> GetSettingsAsync()
        {
            var response = await _http.GetAsync("api/system/settings");
            if (!response.IsSuccessStatusCode)
                return null;
            var result = await response.Content.ReadFromJsonAsync<ApiResult<List<SystemSettingsDto>>>(_jsonOptions);
            return result?.Code == 200 ? result.Data : null;
        }

        public async Task<bool> UpdateSettingsAsync(List<SystemSettingsDto> settings)
        {
            var response = await _http.PutAsync("api/system/settings", settings);
            if (!response.IsSuccessStatusCode)
                return false;
            var result = await response.Content.ReadFromJsonAsync<ApiResult>(_jsonOptions);
            return result?.Code == 200;
        }

        public async Task<bool> ReloadModbusAsync()
        {
            var response = await _http.PostAsync<object>("api/system/modbus/reload", null);
            if (!response.IsSuccessStatusCode)
                return false;
            var result = await response.Content.ReadFromJsonAsync<ApiResult>(_jsonOptions);
            return result?.Code == 200;
        }
    }
}