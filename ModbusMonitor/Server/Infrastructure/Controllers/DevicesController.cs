using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Models.Dtos;
using Server.Services;

namespace Server.Infrastructure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DevicesController : ControllerBase
    {
        private readonly IDeviceService _service;

        public DevicesController(IDeviceService service) => _service = service;

        /// <summary>GET api/devices?page=1&pageSize=10 — 分页获取设备列表</summary>
        [HttpGet]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _service.GetPagedAsync(page, pageSize);
            return Ok(result);
        }

        /// <summary>GET api/devices/{id} — 获取设备详情（含测点列表）</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.Code == 200 ? Ok(result) : NotFound(result);
        }

        /// <summary>POST api/devices — 创建设备</summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateDeviceRequest request)
        {
            var result = await _service.CreateAsync(request);
            return result.Code == 200 ? Ok(result) : BadRequest(result);
        }

        /// <summary>PUT api/devices/{id} — 更新设备</summary>
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDeviceRequest request)
        {
            var result = await _service.UpdateAsync(id, request);
            return result.Code == 200 ? Ok(result) : (result.Code == 404 ? NotFound(result) : BadRequest(result));
        }

        /// <summary>DELETE api/devices/{id} — 删除设备</summary>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _service.DeleteAsync(id);
            return result.Code == 200 ? Ok(result) : (result.Code == 404 ? NotFound(result) : BadRequest(result));
        }
    }
}
