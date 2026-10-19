using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.ToTable("JournalLines");
        builder.HasOne(x => x.JournalEntry).WithMany(x => x.Lines)
                    .HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Account).WithMany(x => x.JournalLines)
                    .HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Debit).HasPrecision(15, 2);
        builder.Property(x => x.Credit).HasPrecision(15, 2);
    }
}
