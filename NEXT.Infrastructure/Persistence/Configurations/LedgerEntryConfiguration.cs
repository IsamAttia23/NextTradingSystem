using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries", table =>
        {
            table.HasCheckConstraint("CK_LedgerEntries_Identifiers", "\"OperationId\" <> '00000000-0000-0000-0000-000000000000' AND \"ReferenceId\" <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_LedgerEntries_Kind", "\"Kind\" BETWEEN 1 AND 9 AND \"ReferenceType\" BETWEEN 1 AND 7");
            table.HasCheckConstraint("CK_LedgerEntries_Source", "(\"ReferenceType\" <> 1 OR \"InvoiceId\" IS NOT DISTINCT FROM \"ReferenceId\") AND (\"ReferenceType\" <> 2 OR \"PaymentId\" IS NOT DISTINCT FROM \"ReferenceId\")");
            table.HasCheckConstraint("CK_LedgerEntries_Reversal", "(\"Kind\" = 6) = (\"ReversesEntryId\" IS NOT NULL) AND (\"ReversesEntryId\" IS NULL OR (\"ReferenceType\" = 4 AND \"ReferenceId\" = \"ReversesEntryId\"))");
            table.HasCheckConstraint("CK_LedgerEntries_PostingKey", "\"PostingKey\" = \"Kind\"::text || ':' || \"ReferenceType\"::text || ':' || replace(\"ReferenceId\"::text, '-', '')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PostingKey).HasMaxLength(160).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ActorName).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => x.PostingKey).IsUnique();
        builder.HasIndex(x => x.ReversesEntryId).IsUnique().HasFilter("\"ReversesEntryId\" IS NOT NULL");
        builder.HasIndex(x => new { x.EntryDate, x.Id });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LedgerEntry>().WithMany().HasForeignKey(x => x.ReversesEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne(x => x.LedgerEntry).HasForeignKey(x => x.LedgerEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
