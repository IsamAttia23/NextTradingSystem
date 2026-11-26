using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Entities;
namespace NEXT.Infrastructure.Persistence.Configurations;
public sealed class FinancialPeriodEventConfiguration : IEntityTypeConfiguration<FinancialPeriodEvent>
{
    public void Configure(EntityTypeBuilder<FinancialPeriodEvent> b) {
        b.ToTable("FinancialPeriodEvents"); b.HasKey(x=>x.Id); b.Property(x=>x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x=>x.ActorName).HasMaxLength(200).IsRequired(); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.ActorId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x=>new {x.PeriodId,x.Version}).IsUnique();
        b.HasOne<FinancialPeriod>().WithMany().HasForeignKey(x=>x.PeriodId).OnDelete(DeleteBehavior.Restrict);
    }
}
