using Server.Models.Common;
using Server.Models.Dtos;

namespace Server.Services
{
    public interface IDeviceService
    {
        Task<ApiResult<PagedResult<DeviceListItemDto>>> GetPagedAsync(int page, int pageSize);
        Task<ApiResult<DeviceDto?>> GetByIdAsync(int id);
        Task<ApiResult<DeviceDto>> CreateAsync(CreateDeviceRequest request);
        Task<ApiResult<DeviceDto>> UpdateAsync(int id, UpdateDeviceRequest request);
        Task<ApiResult> DeleteAsync(int id); 
    }
}
