using Client.Models.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Service
{
    public interface IDeviceService
    {
        Task<PagedResult<DeviceListItemDto>?> GetPagedAsync(int page, int pageSize);
        Task<DeviceDto?> GetByIdAsync(int id);
        Task<bool> CreateAsync(CreateDeviceRequest request);
        Task<bool> UpdateAsync(int id, UpdateDeviceRequest request);
        Task<bool> DeleteAsync(int id);
    }
}
