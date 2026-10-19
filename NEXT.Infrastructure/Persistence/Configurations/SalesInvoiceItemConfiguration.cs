using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class SalesInvoiceItemConfiguration : IEntityTypeConfiguration<SalesInvoiceItem>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceItem> builder)
    {
        builder.ToTable("SalesInvoiceItems");
        builder.HasOne(x => x.Invoice).WithMany(x => x.Items)
                    .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Product).WithMany(x => x.SalesInvoiceItems)
                    .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductBatch).WithMany()
                    .HasForeignKey(x => x.ProductBatchId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.Quantity).HasPrecision(15, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(15, 2);
        builder.Property(x => x.UnitCost).HasPrecision(15, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(15, 2);
        builder.Property(x => x.TaxRate).HasPrecision(5, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(15, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(15, 2);
    }
}
