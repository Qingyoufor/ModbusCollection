using Client.Infrastructure;
using Client.Models.Dtos;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Client.Service
{
    public class AlarmService : IAlarmService
    {
        private readonly HttpClientBase _http;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public AlarmService(HttpClientBase http)
        {
            _http = http;
        }

        public async Task<PagedResult<AlarmDto>?> GetHistoryAsync(int page, int pageSize, int? deviceId, string? status)
        {
            var url = $"api/alarms/history?page={page}&pageSize={pageSize}";
            if (deviceId.HasValue)
                url += $"&deviceId={deviceId.Value}";
            if (!string.IsNullOrEmpty(status))
                url += $"&status={status}";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResult<PagedResult<AlarmDto>>>(_jsonOptions);
            return result?.Code == 200 ? result.Data : null;
        }

        public async Task<bool> AcknowledgeAsync(long alarmId)
        {
            var response = await _http.PutAsync<object>($"api/alarms/{alarmId}/acknowledge", null);
            return response.IsSuccessStatusCode;
        }
    }
}
