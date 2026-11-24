using Microsoft.EntityFrameworkCore;
using SkyMate.Application.Interfaces.Persistence;
using SkyMate.Infrastructure.Persistence;

namespace SkyMate.Infrastructure.Persistence.Repositories;

/// <summary>Implements basic data operations for an EF Core entity type.</summary>
/// <typeparam name="T">The entity type handled by this repository.</typeparam>
public sealed class GenericRepository<T>(AppDbContext dbContext) : IGenericRepository<T> where T : class
{
    private readonly DbSet<T> _dbSet = dbContext.Set<T>();

    /// <summary>Finds an entity by its primary key.</summary>
    /// <returns>The entity, or <see langword="null"/> when it does not exist.</returns>
    public async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _dbSet.FindAsync([id], cancellationToken);

    /// <summary>Lists entities without tracking them for changes.</summary>
    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbSet.AsNoTracking().ToListAsync(cancellationToken);

    /// <summary>Adds an entity to the context.</summary>
    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await _dbSet.AddAsync(entity, cancellationToken);

    /// <summary>Marks an entity as modified.</summary>
    public void Update(T entity) => _dbSet.Update(entity);

    /// <summary>Marks an entity for deletion.</summary>
    public void Remove(T entity) => _dbSet.Remove(entity);
}
