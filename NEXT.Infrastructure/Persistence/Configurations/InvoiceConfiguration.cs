using NEXT.Infrastructure.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasOne(x => x.ParentInvoice).WithMany(x => x.Returns)
                    .HasForeignKey(x => x.ParentInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany(x => x.Invoices)
                    .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId)
                    .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(x => x.User);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CustomerName).HasMaxLength(200);
        builder.Property(x => x.CreatedByFullName).HasMaxLength(200);
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
        builder.Property(x => x.SubTotal).HasPrecision(15, 2);
        builder.Property(x => x.DiscountValue).HasPrecision(15, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(15, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(15, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(15, 2);
        builder.Property(x => x.PaidAmount).HasPrecision(15, 2);
        builder.Property(x => x.RemainingAmount).HasPrecision(15, 2);
        builder.Property(x => x.ChangeAmount).HasPrecision(15, 2);
        builder.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
    }
}
