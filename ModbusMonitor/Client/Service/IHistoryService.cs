using Client.Models.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Service
{
    public interface IHistoryService
    {
        Task<List<HistoryDataPointDto>?> GetHistoryAsync(
            int deviceId, int dataPointId, DateTime start, DateTime end);

        Task<HistoryStatisticsDto?> GetStatisticsAsync(
            int deviceId, int dataPointId, DateTime start, DateTime end);

        Task<byte[]?> ExportAsync(int deviceId, int dataPointId, DateTime start, DateTime end);
    }
}
