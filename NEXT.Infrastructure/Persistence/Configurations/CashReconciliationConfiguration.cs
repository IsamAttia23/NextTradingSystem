using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Entities;
namespace NEXT.Infrastructure.Persistence.Configurations;
public sealed class CashReconciliationConfiguration : IEntityTypeConfiguration<CashReconciliation>
{
    public void Configure(EntityTypeBuilder<CashReconciliation> b) {
        b.ToTable("CashReconciliations",t=>t.HasCheckConstraint("CK_CashReconciliation_Values","\"Account\" IN (5,13,14,15) AND \"DepositsInTransit\" >= 0 AND \"OutstandingPayments\" >= 0 AND (\"Account\" <> 5 OR (\"DepositsInTransit\" = 0 AND \"OutstandingPayments\" = 0))"));
        b.HasKey(x=>x.Id); b.HasIndex(x=>x.OperationId).IsUnique(); b.Ignore(x=>x.Difference);
        b.Property(x=>x.BookBalance).HasPrecision(15,2); b.Property(x=>x.ExternalBalance).HasPrecision(15,2);
        b.Property(x=>x.DepositsInTransit).HasPrecision(15,2); b.Property(x=>x.OutstandingPayments).HasPrecision(15,2);
        b.Property(x=>x.PositionHash).HasMaxLength(64).IsRequired(); b.Property(x=>x.Evidence).HasMaxLength(200).IsRequired();
        b.Property(x=>x.Reason).HasMaxLength(500).IsRequired(); b.Property(x=>x.ActorName).HasMaxLength(200).IsRequired(); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
