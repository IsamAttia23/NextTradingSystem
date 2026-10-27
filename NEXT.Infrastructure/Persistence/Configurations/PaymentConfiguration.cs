using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasOne(x => x.Customer).WithMany()
                    .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supplier).WithMany()
                    .HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SalesInvoice).WithMany()
                    .HasForeignKey(x => x.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PurchaseInvoice).WithMany()
                    .HasForeignKey(x => x.PurchaseInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.PaymentNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.PaymentNumber).IsUnique();
        builder.HasIndex(x => x.PaymentDate);
        builder.Property(x => x.Amount).HasPrecision(15, 2);
        builder.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
    }
}
