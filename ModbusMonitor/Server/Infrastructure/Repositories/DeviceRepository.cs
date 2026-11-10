using Microsoft.EntityFrameworkCore;
using Server.Infrastructure.Data;
using Server.Models.Common;
using Server.Models.Dtos;
using Server.Models.Entities;
using System.Linq.Expressions;

namespace Server.Infrastructure.Repositories
{
    public class DeviceRepository : RepositoryBase<Device>,IDeviceRepository
    {
        public DeviceRepository(ModbusMonitorDbContext context) : base(context) { }

        // 公共投影：设备 → 列表项Dto（GetListItemAsync / GetPagedDtoAsync 共用）
        private static readonly Expression<Func<Device, DeviceListItemDto>> ToListItemDto =
            d => new DeviceListItemDto
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
            };

        public async Task<Device?> GetWithDetailsAsync(int id)
            => await _dbSet.Include(d => d.DataPoints).FirstOrDefaultAsync(d => d.Id == id);

        public Task<List<DeviceListItemDto>> GetListItemAsync()
            => _dbSet.Select(ToListItemDto).ToListAsync();

        public async Task<PagedResult<DeviceListItemDto>> GetPagedDtoAsync(int page, int pageSize)
        {
            var query = _dbSet.OrderBy(d => d.Id);
            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(ToListItemDto).ToListAsync();

            return new PagedResult<DeviceListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<bool> CodeExistsAsync(string code, int? excludeId = null)
        {
            var query = _dbSet.Where(d => d.Code == code);
            // 检查除了自己是否有人用（excludeId有值 => 编辑场景,无值 => 新增场景）
            if (excludeId.HasValue)
            {
                query = query.Where(d => d.Id != excludeId.Value);
            }
            // any:有数据返回true，否则返回false
            return await query.AnyAsync();
        }

    }
}
