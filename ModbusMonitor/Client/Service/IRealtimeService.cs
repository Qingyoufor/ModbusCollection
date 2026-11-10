using Client.Models.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Service
{
    public interface IRealtimeService
    {
        Task<RealtimeDeviceDataDto?> GetDeviceDataAsync(int deviceId);
        Task<RealtimeAllDataDto?> GetAllDataAsync();
    }
}
