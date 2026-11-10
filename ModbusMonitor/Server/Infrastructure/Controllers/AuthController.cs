using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Server.Models.Common;
using Server.Models.Dtos;
using Server.Services;
using System.Security.Claims;

namespace Server.Infrastructure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService; 
        }

        [HttpPost("signIn")]
        public async Task<IActionResult> SignIn([FromBody] SignInRequest request)
        {
            var result = await _authService.LoginAsync(request);
            return (result.Code == 200)? Ok(result) : BadRequest(result);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request);
            return (result.Code == 200) ? Ok(result) : BadRequest(result);
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // 从中间件Authentication解析的Claims（来自Token载荷）中取用户Id
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if(userIdClaim == null)
            {
                return BadRequest(ApiResult<LoginResponse>.Fail(401,"无效的身份凭证"));
            }
            await _authService.RevokeAllTokensAsync(int.Parse(userIdClaim));
            return Ok(ApiResult.Success("注销成功"));
        }
    }
}
