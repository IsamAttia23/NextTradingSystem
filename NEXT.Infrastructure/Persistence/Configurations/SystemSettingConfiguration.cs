using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings").HasKey(x => x.Key);
        builder.Property(x => x.ValueJson).HasColumnType("jsonb");
        builder.Property(x => x.Key).HasMaxLength(150);
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();
    }
}
