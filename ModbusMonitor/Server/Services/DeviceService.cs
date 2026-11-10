using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Server.Infrastructure.Data;
using Server.Infrastructure.Repositories;
using Server.Models.Common;
using Server.Models.Dtos;
using Server.Models.Entities;

namespace Server.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly IDeviceRepository _deviceRepo;
        private readonly IMapper _mapper;
        private readonly ModbusMonitorDbContext _context;

        public DeviceService(IDeviceRepository deviceRepo, IMapper mapper, ModbusMonitorDbContext context)
        {
            _deviceRepo = deviceRepo;
            _mapper = mapper;
            _context = context;
        }
        public async Task<ApiResult<PagedResult<DeviceListItemDto>>> GetPagedAsync(int page, int pageSize)
        {
            var paged = await _deviceRepo.GetPagedDtoAsync(page, pageSize);
            if(paged == null)
            {
                return ApiResult<PagedResult<DeviceListItemDto>>.Fail(404, "此页不存在设备");
            }
            return ApiResult<PagedResult<DeviceListItemDto>>.Success(paged);
        }

        public async Task<ApiResult<DeviceDto?>> GetByIdAsync(int id)
        {
            var device = await _deviceRepo.GetByIdAsync(id);
            if(device == null)
            {
                return ApiResult<DeviceDto?>.Fail(404, "设备不存在");
            }
            return ApiResult<DeviceDto?>.Success(_mapper.Map<DeviceDto>(device));
        }

        public async Task<ApiResult<DeviceDto>> CreateAsync(CreateDeviceRequest request)
        {
            if(await _deviceRepo.CodeExistsAsync(request.Code))
            {
                return ApiResult<DeviceDto>.Fail(400, "设备编码已存在");
            }

            var device = _mapper.Map<Device>(request);
            await _deviceRepo.AddAsync(device);
            await _deviceRepo.SaveChangedAsync();

            return ApiResult<DeviceDto>.Success(_mapper.Map<DeviceDto>(device),"创建成功");
        }

        public async Task<ApiResult<DeviceDto>> UpdateAsync(int id, UpdateDeviceRequest request)
        {
            var device = await _deviceRepo.GetByIdAsync(id);
            if (device == null)
                return ApiResult<DeviceDto>.Fail(404, "设备不存在");

            if(await _deviceRepo.CodeExistsAsync(request.Code, id))
            {
                return ApiResult<DeviceDto>.Fail(400, "设备编码已存在");
            }

            _mapper.Map(request, device);
            await _deviceRepo.SaveChangedAsync();
            return ApiResult<DeviceDto>.Success(_mapper.Map<DeviceDto>(device), "更新成功");
        }

        public async Task<ApiResult> DeleteAsync(int id)
        {
            // 不能用 GetWithDetailsAsync：它会把 DataPoints 一起放进追踪器，
            // 而下面的 ExecuteDeleteAsync 绕过追踪器删行，之后 Remove(device) 会因必填外键被判为已断开而抛异常
            var device = await _deviceRepo.GetByIdAsync(id);
            if (device == null)
                return ApiResult.Fail(404, "设备不存在");

            // 测点、报警、历史数据都以 Restrict 外键挂在设备/测点下，必须按依赖顺序先清子表再删设备
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.Alarms.Where(a => a.DeviceId == id).ExecuteDeleteAsync();
                await _context.DeviceDatas.Where(d => d.DeviceId == id).ExecuteDeleteAsync();
                await _context.DataPoints.Where(p => p.DeviceId == id).ExecuteDeleteAsync();

                await _deviceRepo.DeleteAsync(id);
                await _deviceRepo.SaveChangedAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return ApiResult.Success("删除成功");
        }

    }
}
