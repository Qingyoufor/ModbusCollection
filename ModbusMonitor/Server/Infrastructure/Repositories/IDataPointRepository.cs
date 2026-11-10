using Server.Models.Entities;

namespace Server.Infrastructure.Repositories
{
    public interface IDataPointRepository : IRepositoryBase<DataPoint>
    {
        Task<List<DataPoint>> GetByDeviceIdAsync(int deviceId);
    }
}
