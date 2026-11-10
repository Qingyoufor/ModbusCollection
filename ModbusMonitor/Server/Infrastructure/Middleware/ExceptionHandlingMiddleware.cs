using Server.Models.Common;

namespace Server.Infrastructure.Middleware
{
    /// <summary>全局异常处理：统一返回 ApiResult 格式，开发环境附带异常详情</summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "未处理异常：{Message}", ex.Message);

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                // 开发环境返回详细错误便于排查，生产只返回统一文案
                // 注意：ApiResult.Fail 只有两个参数，异常堆栈已通过上面的 LogError 记录
                var result = _env.IsDevelopment()
                    ? ApiResult.Fail(500, $"服务器内部错误: {ex.Message}")
                    : ApiResult.Fail(500, "服务器内部错误");

                await context.Response.WriteAsJsonAsync(result);
            }
        }
    }
}