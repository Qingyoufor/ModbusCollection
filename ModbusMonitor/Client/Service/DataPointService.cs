using Client.Infrastructure;
using Client.Models.Dtos;
using System.Net.Http.Json;
using System.Text.Json;

namespace Client.Service
{
    public class DataPointService : IDataPointService
    {
        private readonly HttpClientBase _http;

        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public DataPointService(HttpClientBase http)
        {
            _http = http;
        }

        public async Task<List<DataPointDto>?> GetByDeviceIdAsync(int deviceId)
        {
            var response = await _http.GetAsync($"api/datapoints?deviceId={deviceId}");

            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResult<List<DataPointDto>>>(_jsonOptions);

            return result?.Code == 200 ? result.Data : null;
        }

        public async Task<bool> CreateAsync(int deviceId, CreateDataPointRequest request)
        {
            var response = await _http.PostAsync($"api/datapoints?deviceId={deviceId}", request);

            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<ApiResult>(_jsonOptions);

            return result?.Code == 200;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var response = await _http.DeleteAsync($"api/datapoints/{id}");

            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<ApiResult>(_jsonOptions);

            return result?.Code == 200;
        }
    }
}
