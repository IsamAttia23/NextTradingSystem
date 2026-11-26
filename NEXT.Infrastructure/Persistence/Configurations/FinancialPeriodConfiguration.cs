using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Entities;
namespace NEXT.Infrastructure.Persistence.Configurations;
public sealed class FinancialPeriodConfiguration : IEntityTypeConfiguration<FinancialPeriod>
{
    public void Configure(EntityTypeBuilder<FinancialPeriod> b) {
        b.ToTable("FinancialPeriods", t=>t.HasCheckConstraint("CK_FinancialPeriod_Dates", "\"Start\" > DATE '0001-01-01' AND \"End\" >= \"Start\""));
        b.HasKey(x=>x.Id); b.Property(x=>x.Name).HasMaxLength(100).IsRequired(); b.Property(x=>x.Version).IsConcurrencyToken();
        b.HasIndex(x=>x.Start).IsUnique();
    }
}
