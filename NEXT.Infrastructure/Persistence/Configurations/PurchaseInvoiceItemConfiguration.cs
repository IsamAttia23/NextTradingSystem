using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class PurchaseInvoiceItemConfiguration : IEntityTypeConfiguration<PurchaseInvoiceItem>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceItem> builder)
    {
        builder.ToTable("PurchaseInvoiceItems");
        builder.HasOne(x => x.PurchaseInvoice).WithMany(x => x.Items)
                    .HasForeignKey(x => x.PurchaseInvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Product).WithMany()
                    .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductBatch).WithMany()
                    .HasForeignKey(x => x.ProductBatchId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.Quantity).HasPrecision(15, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(15, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(15, 2);
        builder.Property(x => x.TaxRate).HasPrecision(5, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(15, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(15, 2);
    }
}
