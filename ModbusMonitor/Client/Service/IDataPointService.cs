using Client.Models.Dtos;

namespace Client.Service
{
    public interface IDataPointService
    {
        Task<List<DataPointDto>?> GetByDeviceIdAsync(int deviceId);
        Task<bool> CreateAsync(int deviceId, CreateDataPointRequest request);
        Task<bool> DeleteAsync(int id);
    }
}
