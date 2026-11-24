using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using SkyMate.Domain.Entities;
using SkyMate.Infrastructure.Identity;

namespace SkyMate.Infrastructure.Persistence;

/// <summary>EF Core context for Identity and SkyMate travel data.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<Interest> Interests => Set<Interest>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripDay> TripDays => Set<TripDay>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<TripInterest> TripInterests => Set<TripInterest>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiConversationMessage> AiConversationMessages => Set<AiConversationMessage>();
    public DbSet<KnowledgeBaseEntry> KnowledgeBaseEntries => Set<KnowledgeBaseEntry>();

    /// <summary>Rejects overlapping scheduled activities before saving changes.</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateActivitySchedule();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>Rejects overlapping scheduled activities before asynchronously saving changes.</summary>
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        await ValidateActivityScheduleAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Applies Identity settings and all entity configurations from this assembly.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Table and relationship settings are kept in separate configuration classes.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    private void ValidateActivitySchedule()
    {
        var (changedEntries, affectedDayIds) = GetChangedActivities();
        if (changedEntries.Count == 0)
        {
            return;
        }

        var existingActivities = affectedDayIds.Count == 0
            ? []
            : Activities.AsNoTracking().Where(activity => affectedDayIds.Contains(activity.DayId)).ToList();
        EnsureNoOverlaps(changedEntries, existingActivities);
    }

    private async Task ValidateActivityScheduleAsync(CancellationToken cancellationToken)
    {
        var (changedEntries, affectedDayIds) = GetChangedActivities();
        if (changedEntries.Count == 0)
        {
            return;
        }

        var existingActivities = affectedDayIds.Count == 0
            ? []
            : await Activities.AsNoTracking()
                .Where(activity => affectedDayIds.Contains(activity.DayId))
                .ToListAsync(cancellationToken);
        EnsureNoOverlaps(changedEntries, existingActivities);
    }

    private (List<EntityEntry<Activity>> ChangedEntries, List<int> AffectedDayIds) GetChangedActivities()
    {
        ChangeTracker.DetectChanges();
        var changedEntries = ChangeTracker.Entries<Activity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        var affectedDayIds = changedEntries
            .Where(entry => !entry.Property(activity => activity.DayId).IsTemporary)
            .Select(entry => entry.State == EntityState.Deleted
                ? entry.Property(activity => activity.DayId).OriginalValue
                : entry.Property(activity => activity.DayId).CurrentValue)
            .Distinct()
            .ToList();
        return (changedEntries, affectedDayIds);
    }

    private static void EnsureNoOverlaps(
        IReadOnlyCollection<EntityEntry<Activity>> changedEntries,
        IReadOnlyCollection<Activity> existingActivities)
    {
        var changedIds = changedEntries
            .Where(entry => entry.State != EntityState.Added)
            .Select(entry => entry.Entity.Id)
            .ToHashSet();
        var changedDayIds = changedEntries
            .Select(entry => entry.State == EntityState.Deleted
                ? entry.Property(activity => activity.DayId).OriginalValue
                : entry.Property(activity => activity.DayId).CurrentValue)
            .ToHashSet();
        var currentActivities = existingActivities
            .Where(activity => !changedIds.Contains(activity.Id))
            .Concat(changedEntries
                .Where(entry => entry.State != EntityState.Deleted &&
                                changedDayIds.Contains(entry.Property(activity => activity.DayId).CurrentValue))
                .Select(entry => entry.Entity));

        foreach (var activitiesForDay in currentActivities.GroupBy(activity => activity.DayId))
        {
            var scheduled = activitiesForDay
                .Where(activity => activity.StartTime.HasValue && activity.EndTime.HasValue)
                .OrderBy(activity => activity.StartTime)
                .ToList();

            for (var index = 1; index < scheduled.Count; index++)
            {
                if (scheduled[index].StartTime!.Value < scheduled[index - 1].EndTime!.Value)
                {
                    throw new InvalidOperationException(
                        $"Activities {scheduled[index - 1].Id} and {scheduled[index].Id} overlap within trip day {activitiesForDay.Key}.");
                }
            }
        }
    }
}
