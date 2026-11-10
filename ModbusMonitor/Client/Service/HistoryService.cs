using Client.Infrastructure;
using Client.Models.Dtos;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Client.Service
{
    public class HistoryService(HttpClientBase _http) : IHistoryService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public async Task<List<HistoryDataPointDto>?> GetHistoryAsync(
            int deviceId, int dataPointId, DateTime start, DateTime end)
        {
            var url = $"api/history/data?deviceId={deviceId}&pointId={dataPointId}&start={start:O}&end={end:O}";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResult<List<HistoryDataPointDto>>>(_jsonOptions);
            return result?.Code == 200 ? result.Data : null;
        }

        public async Task<HistoryStatisticsDto?> GetStatisticsAsync(
            int deviceId, int dataPointId, DateTime start, DateTime end)
        {
            var url = $"api/history/statistics?deviceId={deviceId}&pointId={dataPointId}&start={start:O}&end={end:O}";
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResult<HistoryStatisticsDto>>(_jsonOptions);
            return result?.Code == 200 ? result.Data : null;
        }

        public async Task<byte[]?> ExportAsync(int deviceId, int dataPointId, DateTime start, DateTime end)
        {
            var url = $"api/history/export?deviceId={deviceId}&pointId={dataPointId}&start={start:O}&end={end:O}";
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsByteArrayAsync();
        }
    }
}
