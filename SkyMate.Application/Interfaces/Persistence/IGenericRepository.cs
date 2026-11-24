namespace SkyMate.Application.Interfaces.Persistence;

/// <summary>Defines basic data operations for one entity type.</summary>
/// <typeparam name="T">The entity type handled by the repository.</typeparam>
public interface IGenericRepository<T> where T : class
{
    /// <summary>Finds an entity by its primary key.</summary>
    /// <returns>The entity, or <see langword="null"/> when it does not exist.</returns>
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lists all entities of this type.</summary>
    Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds an entity so it can be saved with the next unit-of-work commit.</summary>
    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>Marks an entity as modified.</summary>
    void Update(T entity);

    /// <summary>Marks an entity for deletion.</summary>
    void Remove(T entity);
}
