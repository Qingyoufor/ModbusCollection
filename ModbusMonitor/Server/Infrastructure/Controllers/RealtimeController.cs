using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Infrastructure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RealtimeController : ControllerBase
    {
        private readonly IRealtimeService _realtimeService;

        public RealtimeController(IRealtimeService realtimeService)
        {
            _realtimeService = realtimeService;
        }
        [HttpGet("devices/{deviceId}/data")]
        public async Task<IActionResult> GetDeviceData(int deviceId)
        {
            var result = await _realtimeService.GetDeviceDataAsync(deviceId);
            return result.Code == 200 ? Ok(result) : NotFound(result);
        }

        [HttpGet("devices/all")]
        public async Task<IActionResult> GetAllDeviceData()
        {
            var result = await _realtimeService.GetAllDataAsync();
            return Ok(result);
        }
    }
}
