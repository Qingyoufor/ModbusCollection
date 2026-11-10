using Microsoft.EntityFrameworkCore;
using Server.Infrastructure.Data;
using Server.Models.Common;

namespace Server.Infrastructure.Repositories
{
    //CRUD基类
    public class RepositoryBase<TEntity> : IRepositoryBase<TEntity> where TEntity : class
    {
        protected readonly ModbusMonitorDbContext _context;
        protected readonly DbSet<TEntity> _dbSet;

        public RepositoryBase(ModbusMonitorDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<TEntity>();
        }

        //按主键查询 => Find
        public async Task<TEntity?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);

        public async Task<List<TEntity>> GetAllAsync() => await _dbSet.ToListAsync();

        public async Task<TEntity> AddAsync(TEntity entity)
        {
            await _dbSet.AddAsync(entity);
            return entity;
        }

        public void Update(TEntity entity) => _dbSet.Update(entity);

        public async Task DeleteAsync(int id)
        {
            var entityToDelete = await GetByIdAsync(id);
            if (entityToDelete != null) _dbSet.Remove(entityToDelete);
        }

        public async Task SaveChangedAsync() => await _context.SaveChangesAsync();
    }
}
