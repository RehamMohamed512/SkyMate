using SkyMate.Application.Interfaces.Persistence;

namespace SkyMate.Infrastructure.Persistence;

/// <summary>Saves pending changes through the application's EF Core context.</summary>
public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    /// <summary>Saves pending changes asynchronously.</summary>
    /// <returns>The number of state entries written to the database.</returns>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
