using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Models.Common;
using Server.Models.Dtos;
using Server.Services;

namespace Server.Infrastructure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]   // 系统设置属于管理功能，要求登录
    public class SystemController : ControllerBase
    {
        private readonly ISystemService _systemService;

        public SystemController(ISystemService systemService)
        {
            _systemService = systemService;
        }

        /// <summary>系统信息：运行时长 / Modbus 状态 / 在线设备数</summary>
        [HttpGet("info")]
        public async Task<IActionResult> GetInfo()
        {
            var result = await _systemService.GetInfoAsync();
            return Ok(result);
        }


        /// <summary>读取全部系统配置</summary>
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            var result = await _systemService.GetSettingsAsync();
            return Ok(result);
        }

        /// <summary>保存系统配置并触发 Modbus 重载</summary>
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] List<SystemSettingsDto> settings)
        {
            if (settings == null || settings.Count == 0)
                return BadRequest(ApiResult.Fail(400, "配置不能为空"));

            var result = await _systemService.UpdateSettingsAsync(settings);
            return result.Code == 200 ? Ok(result) : BadRequest(result);
        }

        /// <summary>手动重载 Modbus 配置</summary>
        [HttpPost("modbus/reload")]
        public async Task<IActionResult> ReloadModbus()
        {
            var result = await _systemService.ReloadModbusAsync();
            return Ok(result);
        }
    }
}