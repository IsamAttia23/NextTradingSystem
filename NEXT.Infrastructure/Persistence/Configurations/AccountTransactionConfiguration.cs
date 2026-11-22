using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class AccountTransactionConfiguration : IEntityTypeConfiguration<AccountTransaction>
{
    public void Configure(EntityTypeBuilder<AccountTransaction> builder)
    {
        builder.ToTable("AccountTransactions", table =>
        {
            table.HasCheckConstraint("CK_AccountTransactions_Amounts", "\"Debit\" >= 0 AND \"Credit\" >= 0 AND ((\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0))");
            table.HasCheckConstraint("CK_AccountTransactions_Sequence", "\"Sequence\" BETWEEN 1 AND 1000");
            table.HasCheckConstraint("CK_AccountTransactions_Debt", $"(\"AccountId\" = '{Account.IdFor(AccountType.Receivables)}' AND \"DebtItemId\" IS NOT NULL AND \"CustomerId\" IS NOT NULL) OR (\"AccountId\" <> '{Account.IdFor(AccountType.Receivables)}' AND \"DebtItemId\" IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Debit).HasPrecision(15, 2);
        builder.Property(x => x.Credit).HasPrecision(15, 2);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasIndex(x => new { x.LedgerEntryId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.AccountId, x.LedgerEntryId });
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DebtItem).WithMany().HasForeignKey(x => x.DebtItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
