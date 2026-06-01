using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MechanicShop.Infrastructure.Data.Interceptors;

public sealed class AuditableEntityInterceptor(IUser user, TimeProvider time)
    : SaveChangesInterceptor
{
    private readonly IUser _user = user;
    private readonly TimeProvider _time = time;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    )
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>().ToList())
        {
            if ((entry.State is EntityState.Added or EntityState.Modified) || entry.HasChangedOwnedEntities())
            {
                if (entry.State is EntityState.Added)
                {
                    entry.Entity.CreateAtUtc = _time.GetUtcNow();
                    entry.Entity.CreateBy = _user.Id;
                }

                entry.Entity.LastModifiedAtUtc = _time.GetUtcNow();
                entry.Entity.LastModifiedBy = _user.Id;
            }

            foreach (var ownedEntry in entry.References)
            {
                if 
                (
                    ownedEntry.TargetEntry != null &&
                    ownedEntry.TargetEntry!.Entity is AuditableEntity ownedEntity &&
                    ownedEntry.TargetEntry.State is EntityState.Added or EntityState.Modified
                )
                {
                    if (ownedEntry.TargetEntry.State == EntityState.Added)
                    {
                        ownedEntity.CreateBy = _user.Id;
                        ownedEntity.CreateAtUtc = _time.GetUtcNow();
                    }
                    ownedEntity.LastModifiedBy = _user.Id;
                    ownedEntity.LastModifiedAtUtc = _time.GetUtcNow();
                }
            }
        }
    }
}

public static class Extension
{
    public static bool HasChangedOwnedEntities(this EntityEntry entry) =>
        entry.References.Any(r =>
            r.TargetEntry != null
            && r.TargetEntry.Metadata.IsOwned() == true
            && (
                r.TargetEntry.State == EntityState.Added
                || r.TargetEntry.State == EntityState.Modified
            )
        );
}