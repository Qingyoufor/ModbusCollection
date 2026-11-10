using Server.Models.Common;
using Server.Models.Dtos;
using Server.Models.Entities;

namespace Server.Infrastructure.Repositories
{
    public interface IDeviceRepository : IRepositoryBase<Device>
    {
        Task<Device?> GetWithDetailsAsync(int id);                    // 按Id查设备并附带其测点列表（Include DataPoints，详情页用）
        Task<List<DeviceListItemDto>> GetListItemAsync();             // 查全部设备列表项
        Task<PagedResult<DeviceListItemDto>> GetPagedDtoAsync(int page, int pageSize); // 分页查设备列表项
        Task<bool> CodeExistsAsync(string code,int? excludeId = null); // 设备编码是否已存在（查重；excludeId=更新时排除自身）
    }
}
