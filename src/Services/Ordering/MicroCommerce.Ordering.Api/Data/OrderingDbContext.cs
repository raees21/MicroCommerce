using Microsoft.EntityFrameworkCore;

namespace MicroCommerce.Ordering.Api.Data;

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<OrderEventEntity> OrderEvents => Set<OrderEventEntity>();

    public DbSet<OrderSagaStateEntity> OrderSagas => Set<OrderSagaStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderEventEntity>(entity =>
        {
            entity.ToTable("order_events");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrderId, x.Version }).IsUnique();
            entity.Property(x => x.EventType).HasMaxLength(120);
            entity.Property(x => x.Payload).HasColumnType("jsonb");
        });

        modelBuilder.Entity<OrderSagaStateEntity>(entity =>
        {
            entity.ToTable("order_sagas");
            entity.HasKey(x => x.OrderId);
            entity.Property(x => x.CurrentState).HasMaxLength(120);
            entity.Property(x => x.IdempotencyHash).HasMaxLength(128);
        });
    }
}

public sealed class OrderEventEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public int Version { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OrderSagaStateEntity
{
    public Guid OrderId { get; set; }

    public Guid UserId { get; set; }

    public string IdempotencyHash { get; set; } = string.Empty;

    public string CurrentState { get; set; } = "PendingPayment";

    public decimal TotalAmount { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
