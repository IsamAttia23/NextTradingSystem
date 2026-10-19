using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("PurchaseOrderItems");
        builder.HasOne(x => x.PurchaseOrder).WithMany(x => x.Items)
                    .HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Product).WithMany(x => x.PurchaseOrderItems)
                    .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Quantity).HasPrecision(15, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(15, 2);
        builder.Property(x => x.Total).HasPrecision(15, 2);
    }
}
