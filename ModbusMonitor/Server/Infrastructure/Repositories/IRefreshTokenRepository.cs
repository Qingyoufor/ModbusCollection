using Server.Models.Entities;

namespace Server.Infrastructure.Repositories
{
    //Access Token 不是CRUD,Refresh Token 必须实现CRUD
    public interface IRefreshTokenRepository :IRepositoryBase<RefreshToken>
    {
        Task<RefreshToken?> GetByTokenValueAsync(string token);   //获取token
        Task RevokeAllUserTokensAsync(int userId);  //撤销某用户的所有token,强制下线
    }
}
