using Microsoft.EntityFrameworkCore;

namespace MicroCommerce.Shipping.Api.Data;

public sealed class ShippingDbContext(DbContextOptions<ShippingDbContext> options) : DbContext(options)
{
    public DbSet<ShipmentRecordEntity> Shipments => Set<ShipmentRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShipmentRecordEntity>(entity =>
        {
            entity.ToTable("shipments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OrderId).IsUnique();
            entity.Property(x => x.Status).HasMaxLength(64);
            entity.Property(x => x.TrackingNumber).HasMaxLength(64);
        });
    }
}

public sealed class ShipmentRecordEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Guid UserId { get; set; }

    public string TrackingNumber { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
