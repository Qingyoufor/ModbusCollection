using Microsoft.EntityFrameworkCore;
using Server.Infrastructure.Data;
using Server.Models.Entities;

namespace Server.Infrastructure.Repositories
{
    public class DataPointRepository : RepositoryBase<DataPoint>, IDataPointRepository
    {
        public DataPointRepository(ModbusMonitorDbContext context) : base(context) { }

        public async Task<List<DataPoint>> GetByDeviceIdAsync(int deviceId)
            => await _dbSet.Where(dp => dp.DeviceId == deviceId)
            .OrderBy(dp => dp.SortOrder)
            .ToListAsync();
    }
}
