using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class ProductBatchConfiguration : IEntityTypeConfiguration<ProductBatch>
{
    public void Configure(EntityTypeBuilder<ProductBatch> builder)
    {
        builder.ToTable("ProductBatches");
        builder.HasOne(x => x.Product).WithMany(x => x.Batches)
                    .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId)
                    .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ProductId, x.WarehouseId, x.BatchNumber }).IsUnique();
        builder.Property(x => x.Quantity).HasPrecision(15, 3);
        builder.Property(x => x.CostPrice).HasPrecision(15, 2);
    }
}
