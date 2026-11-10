using Microsoft.EntityFrameworkCore;
using Server.Infrastructure.Data;
using Server.Models.Entities;

namespace Server.Infrastructure.Repositories
{
    public class UserRepository : RepositoryBase<User>, IUserRepository
    {
        //通过base(context)基构造函数获取_context和_dbSet(生命周期是Scoped)
        public UserRepository(ModbusMonitorDbContext context) : base(context) { }

        public async Task<User?> GetByUsernameAsync(string username) 
            => await _dbSet.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
    }
}
