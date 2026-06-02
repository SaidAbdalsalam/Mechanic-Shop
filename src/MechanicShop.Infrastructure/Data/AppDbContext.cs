using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IPublisher publisher)
    : IdentityDbContext<AppUser>(options),
        IAppDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Part> Parts => Set<Part>();

    public DbSet<RepairTask> RepairTasks => Set<RepairTask>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public override async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        var entities = ChangeTracker
            .Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();
        var events = entities.SelectMany(e => e.DomainEvents).ToList();

        var result = await base.SaveChangesAsync(ct);

        foreach (var domainEvent in events)
            await publisher.Publish(domainEvent, ct);

        foreach (var entity in entities)
            entity.ClearDomainEvents();
        return result;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
