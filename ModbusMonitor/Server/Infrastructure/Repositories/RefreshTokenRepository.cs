using Microsoft.EntityFrameworkCore;
using Server.Infrastructure.Data;
using Server.Models.Entities;

namespace Server.Infrastructure.Repositories
{
    public class RefreshTokenRepository : RepositoryBase<RefreshToken>, IRefreshTokenRepository
    {
        public RefreshTokenRepository(ModbusMonitorDbContext context) : base(context) { }

        public async Task<RefreshToken?> GetByTokenValueAsync(string tokenValue)
            => await _dbSet.FirstOrDefaultAsync(t => t.Token == tokenValue
               && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow);

        public async Task RevokeAllUserTokensAsync(int userId)
            // ExecuteUpdateAsync：一条 UPDATE SQL 直接批量撤销，不加载实体进内存
            => await _dbSet
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, DateTime.UtcNow));
    }
}
