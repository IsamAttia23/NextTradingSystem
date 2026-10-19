using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class RecycleBinEntryConfiguration : IEntityTypeConfiguration<RecycleBinEntry>
{
    public void Configure(EntityTypeBuilder<RecycleBinEntry> builder)
    {
        builder.ToTable("RecycleBinEntries");
        builder.Property(x => x.EntityDataJson).HasColumnType("jsonb");
        builder.Property(x => x.EntityType).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
