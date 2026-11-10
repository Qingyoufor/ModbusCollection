using Server.Models.Common;

namespace Server.Infrastructure.Repositories
{
    //实现CRUD基接口
    //where TEntity : class 约束TEntity必须是引用类型
    public interface IRepositoryBase<TEntity> where TEntity : class
    {
        Task<TEntity?> GetByIdAsync(int id); //对象可空 null
        Task<List<TEntity>> GetAllAsync();  //可空列表 empty
        Task<TEntity> AddAsync(TEntity entity);
        void Update(TEntity entity);
        Task DeleteAsync(int id);
        Task SaveChangedAsync();
    }
}
