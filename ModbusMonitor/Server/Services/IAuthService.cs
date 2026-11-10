using Microsoft.AspNetCore.Identity.Data;
using Server.Models.Common;
using Server.Models.Dtos;

namespace Server.Services
{
    public interface IAuthService
    {
        //登录
        Task<ApiResult<LoginResponse>> LoginAsync(SignInRequest request);

        //刷新token
        Task<ApiResult<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request);

        //撤销所有token
        Task RevokeAllTokensAsync(int userId);
    }
}
