using Microsoft.IdentityModel.Tokens;
using Server.Models.Common;
using Server.Models.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Server.Infrastructure.Auth
{
    public class JwtHelper
    {
        /// <summary>
        /// 生成AccessToken
        /// </summary>
        public static string GenerateAccessToken(User user,IConfiguration jwtConfig)
        {
            //声明
            var claims = new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),
                new Claim(ClaimTypes.Name,user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // JWT Id 用于避免重放攻击
            };

            //签名密钥-证书
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig["Key"]!));
            var creds = new SigningCredentials(key,SecurityAlgorithms.HmacSha256);

            //生成token对象
            var token = new JwtSecurityToken(
                issuer: jwtConfig["Issuer"],
                audience: jwtConfig["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(int.Parse(jwtConfig["ExpireMinutes"]!)),
                signingCredentials: creds);

            //序列化token，返回token字符串
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// 生成RefreshToken
        /// </summary>
        public static string GenerateRefreshValueToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));


        /// <summary>
        /// 从过期accesstoken获取claims（userId、role...）（通过相同的key解析实现）
        /// </summary>
        public static ClaimsPrincipal GetClaimsPrincipalFromExpiredToken(string accessToken, string secretkey)
        {
            var tokenvalidationParameters = new TokenValidationParameters
            {
                // 只验证签名密钥，不验证Issuer/Audience/Lifetime
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretkey)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ClockSkew = TimeSpan.Zero,
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(accessToken, tokenvalidationParameters,out _);

            return principal; // jwt负载的对象化
        }
    }

    
}
