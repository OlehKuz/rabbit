using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace STP.Interfaces
{
    public interface IRepository<TEntity, TKey> where TEntity : class, IEntity<TKey>
    {
        Task<TEntity[]> GetAllAsync();
        Task<TEntity> FindAsync(TKey key);
        Task<TEntity[]> GetAsync(Expression<Func<TEntity, bool>> expression);
        Task<TEntity> InsertAsync(TEntity item);
        Task<bool> IsExistAsync(TKey key);
        TEntity Update(TEntity item);
        bool Remove(TEntity item);
        bool Remove(TKey key);
        IUnitOfWork UnitOfWork { get; }
    }
}
