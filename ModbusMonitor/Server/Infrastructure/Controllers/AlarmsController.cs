using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Server.Services;
using System.Security.Claims;

namespace Server.Infrastructure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AlarmsController : ControllerBase
    {
        private readonly IAlarmService _alarmService;
        public AlarmsController(IAlarmService alarmService)
        {
            _alarmService = alarmService;  
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentAlarms()
        {
            var result = await _alarmService.GetCurrentAlarmsAsync();
            return Ok(result);
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? deviceId = null,

            [FromQuery] string? status = null)
        {
            var result = await _alarmService.GetHistoryAsync(page, pageSize, deviceId, status);
            return Ok(result);
        }

        [HttpPut("{id}/acknowledge")]
        public async Task<IActionResult> Acknowledge(long id)
        {
            // 从 JWT Token 中获取当前用户名
            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

            var result = await _alarmService.AcknowledgeAsync(id, username);
            return result.Code == 200 ? Ok(result) : BadRequest(result);
        }
    }
}
