using AutoMapper;
using Server.Infrastructure.Auth;
using Server.Infrastructure.Repositories;
using Server.Models.Common;
using Server.Models.Dtos;
using Server.Models.Entities;
using System.Security.Claims;

namespace Server.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IRefreshTokenRepository _refreshTokenRepo;
        private IMapper _mapper;
        private readonly IConfiguration _jwtConfig;

        public AuthService(IUserRepository userRepo,IRefreshTokenRepository tokenRepo,IMapper mapper,IConfiguration jwtConfig)
        {
            _userRepo = userRepo;
            _refreshTokenRepo = tokenRepo;
            _mapper = mapper;
            _jwtConfig = jwtConfig;
        }

        /// <summary>
        /// 登陆
        /// </summary>
        public async Task<ApiResult<LoginResponse>> LoginAsync(SignInRequest request)
        {
            // 1.查找用户 + 校验密码
            var user = await _userRepo.GetByUsernameAsync(request.Username);
            if(user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return ApiResult<LoginResponse>.Fail(401, "账号或密码错误");
            }

            // 2.更新保存最后登录时间
            user.LastLoginAt = DateTime.UtcNow;
            _userRepo.Update(user);
            await _userRepo.SaveChangedAsync();

            // 3.签发双token
            var jwtSection = _jwtConfig.GetSection("Jwt");
            var accessToken = JwtHelper.GenerateAccessToken(user, jwtSection);
            var refreshTokenValue = JwtHelper.GenerateRefreshValueToken();

            // 4.保存RefreshToken
            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshTokenValue,
                ExpiresAt = DateTime.UtcNow.AddDays(double.Parse(jwtSection["RefreshExpireDays"]!)),
                CreatedAt = DateTime.UtcNow
            };
            await _refreshTokenRepo.AddAsync(refreshToken);
            await _refreshTokenRepo.SaveChangedAsync();

            // 5.返回响应
            return ApiResult<LoginResponse>.Success(new LoginResponse
            {
                // 复制到Bearer请求头
                AccseeToken = accessToken,
                RefreshToken = refreshTokenValue,
                ExpiresAt = DateTime.UtcNow.AddMinutes(int.Parse(jwtSection["ExpireMinutes"]!)),
                User = _mapper.Map<UserDto>(user)
            });
            
        }

        /// <summary>
        /// 刷新access/refresh token，第八天强制重新登录（因为过了RefreshToken的ExpiredAt）
        /// </summary>
        public async Task<ApiResult<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request)
        {
            var jwtSection = _jwtConfig.GetSection("Jwt");

            // 1.从过期的AccessToken读取principal（claims）获取userId（判断Access/Refresh Token）
            var principal = JwtHelper.GetClaimsPrincipalFromExpiredToken(request.AccessToken, jwtSection["Key"]!);

            var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if(userIdStr == null)
            {
                return ApiResult<LoginResponse>.Fail(401, "无效的访问令牌");
            }

            // 2.查RefreshToken是否有效（未过期 + 未撤销，这两步操作已在仓库层完成）
            var storedToken = await _refreshTokenRepo.GetByTokenValueAsync(request.RefreshToken);
            if(storedToken == null || storedToken.UserId.ToString() != userIdStr)
            {
                return ApiResult<LoginResponse>.Fail(401, "无效的刷新令牌");
            }

            // 3.撤销旧的RefreshToken(RefreshToken只用一次)
            storedToken.RevokedAt = DateTime.UtcNow;

            // 4.查找用户并签发新的双令牌
            var user = await _userRepo.GetByIdAsync(int.Parse(userIdStr));
            if(user == null || !user.IsActive)
            {
                return ApiResult<LoginResponse>.Fail(401, "用户不存在或已禁用");
            }

            var newAccessToken = JwtHelper.GenerateAccessToken(user, jwtSection);
            var newRefreshTokenValue = JwtHelper.GenerateRefreshValueToken();
            var newRefreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = newRefreshTokenValue, 
                ExpiresAt = DateTime.UtcNow.AddDays(double.Parse(jwtSection["RefreshExpireDays"]!)),
                CreatedAt = DateTime.UtcNow
            };
            await _refreshTokenRepo.AddAsync(newRefreshToken);
            await _refreshTokenRepo.SaveChangedAsync();

            return ApiResult<LoginResponse>.Success(new LoginResponse
            {
                AccseeToken = newAccessToken,
                RefreshToken = newRefreshTokenValue,
                ExpiresAt = DateTime.UtcNow.AddMinutes(int.Parse(jwtSection["ExpireMinutes"]!)),
                User = _mapper.Map<UserDto>(user)
            });
        }

        /// <summary>
        /// 撤销所有RefreshToken，强制下线
        /// </summary>
        public async Task RevokeAllTokensAsync(int userId)
        {
            await _refreshTokenRepo.RevokeAllUserTokensAsync(userId);
            await _refreshTokenRepo.SaveChangedAsync();
        }
        
    }
}
