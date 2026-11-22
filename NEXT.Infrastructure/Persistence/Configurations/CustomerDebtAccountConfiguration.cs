using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class CustomerDebtAccountConfiguration : IEntityTypeConfiguration<CustomerDebtAccount>
{
    public void Configure(EntityTypeBuilder<CustomerDebtAccount> builder)
    {
        builder.ToTable("CustomerDebtAccounts");
        builder.HasOne(x => x.Customer).WithOne().HasForeignKey<CustomerDebtAccount>(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
    }
}
