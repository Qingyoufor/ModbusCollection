using AutoMapper;
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

        public DeviceService(IDeviceRepository deviceRepo, IMapper mapper)
        {
            _deviceRepo = deviceRepo;
            _mapper = mapper;
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
            var device = await _deviceRepo.GetWithDetailsAsync(id);
            if (device == null)
                return ApiResult.Fail(404, "设备不存在");
            // 如果设备下有测点则不允许删除
            if(device.DataPoints.Any())
                return ApiResult.Fail(400, "该设备下存在测点配置，请先删除测点");

            await _deviceRepo.DeleteAsync(id);
            await _deviceRepo.SaveChangedAsync();
            return ApiResult.Success("删除成功");
        }

    }
}
