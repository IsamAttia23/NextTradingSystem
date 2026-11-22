using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class DebtItemConfiguration : IEntityTypeConfiguration<DebtItem>
{
    public void Configure(EntityTypeBuilder<DebtItem> builder)
    {
        builder.ToTable("DebtItems", table =>
        {
            table.HasCheckConstraint("CK_DebtItems_Amount", "\"OriginalAmount\" > 0");
            table.HasCheckConstraint("CK_DebtItems_Source", "(\"Kind\" = 1 AND \"InvoiceId\" IS NOT NULL) OR (\"Kind\" = 2 AND \"InvoiceId\" IS NULL)");
        });
        builder.Property(x => x.OriginalAmount).HasPrecision(15, 2);
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.HasOne(x => x.DebtAccount).WithMany().HasForeignKey(x => x.DebtAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.InvoiceId).IsUnique().HasFilter("\"InvoiceId\" IS NOT NULL");
        builder.HasIndex(x => new { x.DebtAccountId, x.EffectiveDate, x.Id });
    }
}
