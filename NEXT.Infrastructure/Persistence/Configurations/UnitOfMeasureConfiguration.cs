using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ToTable("Units");
        builder.HasOne(x => x.BaseUnit).WithMany(x => x.DerivedUnits)
                    .HasForeignKey(x => x.BaseUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Symbol).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ConversionFactor).HasPrecision(15, 6);
    }
}
