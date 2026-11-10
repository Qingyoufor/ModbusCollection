using Client.Infrastructure;
using Client.Models.Dtos;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Client.Service
{
    public class RealtimeService : IRealtimeService
    {
        private readonly HttpClientBase _http;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public RealtimeService(HttpClientBase http)
        {
            _http = http;   
        }
        public async Task<RealtimeAllDataDto?> GetAllDataAsync()
        {
            var response = await _http.GetAsync("api/realtime/devices/all");
            if(!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<ApiResult<RealtimeAllDataDto>>(_jsonOptions);
            var r = result?.Code;
            return result?.Code == 200 ? result.Data : null;
        }

        public async Task<RealtimeDeviceDataDto?> GetDeviceDataAsync(int deviceId)
        {
            var response = await _http.GetAsync($"api/realtime/devices/{deviceId}/data");
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<ApiResult<RealtimeDeviceDataDto>>(_jsonOptions);
            return result?.Code == 200 ? result.Data : null;
        }
    }
}
