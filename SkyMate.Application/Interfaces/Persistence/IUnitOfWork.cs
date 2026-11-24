namespace SkyMate.Application.Interfaces.Persistence;

/// <summary>Defines the shared save operation for pending data changes.</summary>
public interface IUnitOfWork
{
    /// <summary>Saves pending changes asynchronously.</summary>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
