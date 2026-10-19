using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", "security");
        builder.Property(x => x.Code).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Module).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}
