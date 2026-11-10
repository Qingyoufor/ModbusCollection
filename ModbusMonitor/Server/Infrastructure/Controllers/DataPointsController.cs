using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Models.Dtos;
using Server.Services;

namespace Server.Infrastructure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataPointsController : ControllerBase
    {
        private readonly IDataPointService _service;

        public DataPointsController(IDataPointService service) => _service = service;

        /// <summary>GET api/data-points?deviceId=1 — 获取某设备下的所有测点</summary>
        [HttpGet]
        public async Task<IActionResult> GetByDevice([FromQuery] int deviceId)
        {
            var result = await _service.GetByDeviceIdAsync(deviceId);
            return result.Code == 200 ? Ok(result) : NotFound(result);
        }

        /// <summary>POST api/data-points?deviceId=1 — 为某设备添加测点</summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromQuery] int deviceId, [FromBody] CreateDataPointRequest request)
        {
            var result = await _service.CreateAsync(deviceId, request);
            return result.Code == 200 ? Ok(result) : BadRequest(result);
        }

        /// <summary>PUT api/data-points/{id} — 更新测点</summary>
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDataPointRequest request)
        {
            var result = await _service.UpdateAsync(id, request);
            return result.Code == 200 ? Ok(result) : (result.Code == 404 ? NotFound(result) : BadRequest(result));
        }

        /// <summary>DELETE api/data-points/{id} — 删除测点</summary>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _service.DeleteAsync(id);
            return result.Code == 200 ? Ok(result) : (result.Code == 404 ? NotFound(result) : BadRequest(result));
        }
    }
}
