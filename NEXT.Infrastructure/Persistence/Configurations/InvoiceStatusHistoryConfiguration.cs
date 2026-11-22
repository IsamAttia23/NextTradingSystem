using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class InvoiceStatusHistoryConfiguration : IEntityTypeConfiguration<InvoiceStatusHistory>
{
    public void Configure(EntityTypeBuilder<InvoiceStatusHistory> builder)
    {
        builder.ToTable("InvoiceStatusHistory");
        builder.HasOne(item => item.Invoice).WithMany().HasForeignKey(item => item.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.ActorFullName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Reason).HasMaxLength(500);
        builder.Property(item => item.PaymentAmount).HasPrecision(15, 2);
        builder.HasIndex(item => new { item.InvoiceId, item.OperationId }).IsUnique();
        builder.HasIndex(item => new { item.InvoiceId, item.ActionAt });
    }
}
