using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts", table =>
        {
            table.HasCheckConstraint("CK_Accounts_Type", "\"Type\" BETWEEN 1 AND 15");
            table.HasCheckConstraint("CK_Accounts_Category", "\"Category\" BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Accounts_NormalSide", "\"NormalSide\" IN (0, 1)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Type).IsUnique();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasData(Account.SeedData());
    }
}
