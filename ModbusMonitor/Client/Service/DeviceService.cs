using Client.Infrastructure;
using Client.Models.Dtos;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Client.Service
{
    public class DeviceService : IDeviceService
    {
        private readonly HttpClientBase _http;
        
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public DeviceService(HttpClientBase http)
        {
            _http = http; 
        }


        public async Task<PagedResult<DeviceListItemDto>?> GetPagedAsync(int page, int pageSize)
        {
            var response = await _http.GetAsync($"api/devices?page={page}&pageSize={pageSize}");

            if(!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResult<PagedResult<DeviceListItemDto>>>(_jsonOptions);
            
            return result?.Code == 200?result.Data : null;
        }

        public async Task<DeviceDto?> GetByIdAsync(int id)
        {
            var response = await _http.GetAsync($"api/devices/{id}");

            if(!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResult<DeviceDto?>>(_jsonOptions);

            return result?.Code == 200 ? result.Data : null;
        }

        public async Task<bool> CreateAsync(CreateDeviceRequest request)
        {
            var response = await _http.PostAsync("api/devices",request);

            if(!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<ApiResult>(_jsonOptions);

            return result?.Code == 200;
        }

        public async Task<bool> UpdateAsync(int id, UpdateDeviceRequest request)
        {
            var response = await _http.PutAsync($"api/devices/{id}", request);

            if(!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<ApiResult>(_jsonOptions);

            return result?.Code == 200;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var response = await _http.DeleteAsync($"api/devices/{id}");
            if (!response.IsSuccessStatusCode)
                return false;
            var result = await response.Content.ReadFromJsonAsync<ApiResult>(_jsonOptions);
            return result?.Code == 200;
        }
    }
}
