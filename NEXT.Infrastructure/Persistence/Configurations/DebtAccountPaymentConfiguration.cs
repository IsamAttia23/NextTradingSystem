using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Entities;
namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class DebtAccountPaymentConfiguration : IEntityTypeConfiguration<DebtAccountPayment>
{
    public void Configure(EntityTypeBuilder<DebtAccountPayment> b)
    {
        b.ToTable("DebtAccountPayments", t => t.HasCheckConstraint("CK_DebtAccountPayment_Valid", "\"Amount\" > 0 AND \"CashAccount\" IN (5,13,14,15) AND length(\"RequestHash\") = 64"));
        b.HasKey(d => d.Id);
        b.Property(d => d.Amount).HasPrecision(15, 2);
        b.Property(d => d.Number).HasMaxLength(50).IsRequired();
        b.HasIndex(d => d.Number).IsUnique();
        b.Property(d => d.Notes).HasMaxLength(500);
        b.Property(d => d.RequestHash).HasMaxLength(64).IsRequired();
        b.Property(d => d.ActorName).HasMaxLength(200).IsRequired();
        b.HasOne<CustomerDebtAccount>().WithMany().HasForeignKey(d => d.DebtAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
