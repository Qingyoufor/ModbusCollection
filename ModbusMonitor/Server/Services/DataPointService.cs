using AutoMapper;
using Server.Infrastructure.Repositories;
using Server.Models.Common;
using Server.Models.Dtos;
using Server.Models.Entities;

namespace Server.Services
{
    public class DataPointService : IDataPointService
    {
        private readonly IDataPointRepository _pointRepo;
        private readonly IDeviceRepository _deviceRepo;
        private readonly IMapper _mapper;

        public DataPointService(IDataPointRepository repo, IDeviceRepository deviceRepo, IMapper mapper)
        {
            _pointRepo = repo;
            _deviceRepo = deviceRepo;
            _mapper = mapper;
        }

        public async Task<ApiResult<List<DataPointDto>>> GetByDeviceIdAsync(int deviceId)
        {
            // 先确认设备存在
            var device = await _deviceRepo.GetByIdAsync(deviceId);
            if (device == null)
                return ApiResult<List<DataPointDto>>.Fail(404, "设备不存在");

            var points = await _pointRepo.GetByDeviceIdAsync(deviceId);
            return ApiResult<List<DataPointDto>>.Success(_mapper.Map<List<DataPointDto>>(points));
        }

        public async Task<ApiResult<DataPointDto>> CreateAsync(int deviceId, CreateDataPointRequest request)
        {
            // 先确认设备存在
            var device = await _deviceRepo.GetByIdAsync(deviceId);
            if (device == null)
                return ApiResult<DataPointDto>.Fail(404, "设备不存在");

            var point = _mapper.Map<DataPoint>(request);
            point.DeviceId = deviceId;
            await _pointRepo.AddAsync(point);
            await _pointRepo.SaveChangedAsync();
            return ApiResult<DataPointDto>.Success(_mapper.Map<DataPointDto>(point), "创建成功");
        }

        public async Task<ApiResult<DataPointDto>> UpdateAsync(int id, UpdateDataPointRequest request)
        {
            var point = await  _pointRepo.GetByIdAsync(id);
            if (point == null)
                return ApiResult<DataPointDto>.Fail(404, "测点不存在");

            _mapper.Map(request, point); //request Dto 合并到 point实体
            await _pointRepo.SaveChangedAsync();

            return ApiResult<DataPointDto>.Success(_mapper.Map<DataPointDto>(point), "更新成功");
        }

        public async Task<ApiResult> DeleteAsync(int id)
        {
            var point = await _pointRepo.GetByIdAsync(id);
            if (point == null)
                return ApiResult.Fail(404, "数据点不存在");

            await _pointRepo.DeleteAsync(id);
            await _pointRepo.SaveChangedAsync();
            return ApiResult.Success("删除成功");
        }
    }
}
