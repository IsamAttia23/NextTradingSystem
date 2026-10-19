using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.Property(x => x.OldValuesJson).HasColumnType("jsonb");
        builder.Property(x => x.NewValuesJson).HasColumnType("jsonb");
        builder.HasOne(x => x.User).WithMany()
                    .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.EntityType).HasMaxLength(150).IsRequired();
        builder.Property(x => x.UserAgent).HasMaxLength(1000);
        builder.HasIndex(x => x.CreatedAt);
    }
}
