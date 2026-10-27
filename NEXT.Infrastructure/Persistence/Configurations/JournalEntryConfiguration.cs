using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries");
        builder.Property(x => x.EntryNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.EntryNumber).IsUnique();
        builder.Property(x => x.ReferenceType).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        builder.Property(x => x.TotalDebit).HasPrecision(15, 2);
        builder.Property(x => x.TotalCredit).HasPrecision(15, 2);
        builder.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
    }
}
