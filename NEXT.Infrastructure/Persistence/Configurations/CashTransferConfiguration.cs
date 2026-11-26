using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Entities;
namespace NEXT.Infrastructure.Persistence.Configurations;
public sealed class CashTransferConfiguration : IEntityTypeConfiguration<CashTransfer>
{
    public void Configure(EntityTypeBuilder<CashTransfer> b) {
        b.ToTable("CashTransfers", t=>t.HasCheckConstraint("CK_CashTransfer_Values", "\"FromAccount\" IN (5,13,14,15) AND \"ToAccount\" IN (5,13,14,15) AND \"FromAccount\" <> \"ToAccount\" AND \"Amount\" > 0 AND \"OperationId\" <> '00000000-0000-0000-0000-000000000000'"));
        b.HasKey(x=>x.Id); b.HasIndex(x=>x.OperationId).IsUnique(); b.Property(x=>x.Amount).HasPrecision(15,2);
        b.Property(x=>x.Reason).HasMaxLength(500).IsRequired(); b.Property(x=>x.ActorName).HasMaxLength(200).IsRequired(); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
