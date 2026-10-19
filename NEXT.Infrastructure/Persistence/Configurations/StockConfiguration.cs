using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("Stocks");
        builder.HasOne(x => x.Product).WithMany(x => x.Stocks)
                    .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany(x => x.Stocks)
                    .HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ProductId, x.WarehouseId }).IsUnique();
        builder.Ignore(x => x.AvailableQuantity);
        builder.Property(x => x.Quantity).HasPrecision(15, 3);
        builder.Property(x => x.ReservedQuantity).HasPrecision(15, 3);
        builder.Property(x => x.AverageCost).HasPrecision(15, 2);
    }
}
