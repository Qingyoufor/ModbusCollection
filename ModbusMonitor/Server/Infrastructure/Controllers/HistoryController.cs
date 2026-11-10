using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Services;
using System.Text;

namespace Server.Infrastructure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class HistoryController(IHistoryService _historyService) : ControllerBase
    {
        [HttpGet("data")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] int deviceId,
            [FromQuery] int pointId,
            [FromQuery] DateTime start,
            [FromQuery] DateTime end)
        {
            var result = await _historyService.GetHistoryAsync(deviceId, pointId, start, end);
            return result.Code == 200 ? Ok(result) : BadRequest(result);
        }


        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics(
            [FromQuery] int deviceId,
            [FromQuery] int pointId,
            [FromQuery] DateTime start,
            [FromQuery] DateTime end)
        {
            var result = await _historyService.GetStatisticsAsync(deviceId, pointId, start, end);
            return result.Code == 200 ? Ok(result) : BadRequest(result);
        }


        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] int deviceId,
            [FromQuery] int pointId,
            [FromQuery] DateTime start,
            [FromQuery] DateTime end)
        {
            var result = await _historyService.GetHistoryAsync(deviceId, pointId, start, end);
            if (result.Code != 200 || result.Data == null)
                return BadRequest(result);

            var sb = new StringBuilder();
            sb.AppendLine("设备,测点,时间,数值");

            foreach (var item in result.Data)
            {
                // 库里存的是 UTC，且 datetime2 读回来 Kind 为 Unspecified，需先标记 UTC 再转本地
                var recordedAt = DateTime.SpecifyKind(item.RecordedAt, DateTimeKind.Utc).ToLocalTime();

                sb.AppendLine(
                    $"{EscapeCsv(item.DeviceName)},{EscapeCsv(item.DataPointName)}," +
                    $"{recordedAt:yyyy-MM-dd HH:mm:ss},{item.Value}");
            }

            // 前面加 BOM，让 Excel 打开 CSV 时正确识别 UTF-8 中文
            var bytes = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            return File(bytes, "text/csv; charset=utf-8", $"history_{deviceId}_{pointId}.csv");
        }

        /// <summary>CSV 转义：含逗号/引号/换行的字段用双引号包裹，内部引号翻倍</summary>
        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
