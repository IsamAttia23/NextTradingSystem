using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions", "security").HasKey(x => new { x.RoleId, x.PermissionId });
        builder.HasOne(x => x.Role).WithMany(x => x.RolePermissions)
                    .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Permission).WithMany(x => x.RolePermissions)
                    .HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
