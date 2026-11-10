using Server.Models.Entities;

namespace Server.Infrastructure.Repositories
{
    public interface IUserRepository : IRepositoryBase<User>
    {
        //在拥有IRepositoryBase<User> CRUD的基础上新增 GetByUsernameAsync（）
        Task<User?> GetByUsernameAsync(string username);
    }
}
