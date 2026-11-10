using AutoMapper;
using Server.Infrastructure.Repositories;
using Server.Models.Common;
using Server.Models.Dtos;

namespace Server.Services
{
    /// <summary>报警查询/管理服务：面向前端操作员（报警列表、历史筛选、确认），与后台的 AlarmDetectionService（报警生成）是生产/消费两端</summary>
    public class AlarmService : IAlarmService
    {
        private readonly IAlarmRepository _alarmRepo;
        private readonly IMapper _mapper;

        public AlarmService(IAlarmRepository alarmRepo, IMapper mapper)
        {
            _alarmRepo = alarmRepo;
            _mapper = mapper;
        }
        
        /// <summary>当前未确认的报警列表（报警页首屏数据）</summary>
        public async Task<ApiResult<List<AlarmDto>>> GetCurrentAlarmsAsync()
        {
            var alarms = await _alarmRepo.GetCurrentAlarmsAsync();
            var alarmDtos = _mapper.Map<List<AlarmDto>>(alarms);
            return ApiResult<List<AlarmDto>>.Success(alarmDtos);
        }

        /// <summary>分页查询历史报警，支持按设备/等级/状态筛选（枚举串非法时忽略该条件）</summary>
        public async Task<ApiResult<PagedResult<AlarmDto>>> GetHistoryAsync(
            int page,
            int pageSize,
            int? deviceId, 

            string? status)
        {
            // TryParse：非法枚举串（拼错/恶意）静默忽略该筛选条件，而非抛 500

            AlarmStatus? statusEnum = status != null && Enum.TryParse<AlarmStatus>(status, out var st) ? st : null;

            var paged = await _alarmRepo.GetHistoryAsync(page, pageSize, deviceId, statusEnum);
            var dtoItems = _mapper.Map<List<AlarmDto>>(paged.Items);

            return ApiResult<PagedResult<AlarmDto>>.Success(new PagedResult<AlarmDto>
            {
                Items = dtoItems,
                TotalCount = paged.TotalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        /// <summary>确认报警（操作员点"确认"）：确认后同故障再超限会生成新报警（重置去重）</summary>
        public async Task<ApiResult> AcknowledgeAsync(long alarmId, string acknowledgedBy)
        {
            var success = await _alarmRepo.AcknowledgeAsync(alarmId, acknowledgedBy);
            if (!success)
                return ApiResult.Fail(400, "报警不存在或已确认");

            return ApiResult.Success("报警已确认");
        }
    }
}
