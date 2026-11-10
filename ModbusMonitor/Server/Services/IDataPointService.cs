using Server.Models.Common;
using Server.Models.Dtos;

namespace Server.Services
{
    public interface IDataPointService
    {
        Task<ApiResult<List<DataPointDto>>> GetByDeviceIdAsync(int deviceId);
        Task<ApiResult<DataPointDto>> CreateAsync(int deviceId, CreateDataPointRequest request);
        Task<ApiResult<DataPointDto>> UpdateAsync(int id, UpdateDataPointRequest request);
        Task<ApiResult> DeleteAsync(int id);
    }
}
