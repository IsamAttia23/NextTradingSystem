using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasOne(x => x.Product).WithMany(x => x.StockMovements)
                    .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany(x => x.StockMovements)
                    .HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ProductId, x.WarehouseId, x.CreatedAt });
        builder.Property(x => x.Quantity).HasPrecision(15, 3);
        builder.Property(x => x.UnitCost).HasPrecision(15, 2);
    }
}
