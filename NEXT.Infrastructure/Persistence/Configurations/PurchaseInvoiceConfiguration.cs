using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        builder.ToTable("PurchaseInvoices");
        builder.HasOne(x => x.Supplier).WithMany()
                    .HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany()
                    .HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PurchaseOrder).WithMany()
                    .HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
        builder.Property(x => x.SubTotal).HasPrecision(15, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(15, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(15, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(15, 2);
        builder.Property(x => x.PaidAmount).HasPrecision(15, 2);
        builder.Property(x => x.RemainingAmount).HasPrecision(15, 2);
        builder.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
    }
}
