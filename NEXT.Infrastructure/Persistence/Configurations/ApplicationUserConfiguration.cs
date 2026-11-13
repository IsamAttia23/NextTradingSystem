using NEXT.Infrastructure.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users", "security");
        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.NormalizedUserName).IsUnique();
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.HasOne(x => x.Branch).WithMany()
            .HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
    }
}
