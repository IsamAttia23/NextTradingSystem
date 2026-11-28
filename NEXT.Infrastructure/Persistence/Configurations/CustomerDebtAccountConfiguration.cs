using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class CustomerDebtAccountConfiguration : IEntityTypeConfiguration<CustomerDebtAccount>
{
    public void Configure(EntityTypeBuilder<CustomerDebtAccount> builder)
    {
        builder.ToTable("CustomerDebtAccounts");
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CustomerId).IsUnique().HasFilter("\"ClosedAt\" IS NULL");
        builder.HasIndex(x => new { x.CustomerId, x.CycleNumber }).IsUnique();
        builder.Property(x => x.CycleNumber).HasDefaultValue(1);
        builder.HasOne<NEXT.Infrastructure.Identity.Entities.ApplicationUser>().WithMany().HasForeignKey(x => x.ClosedBy).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
    }
}
