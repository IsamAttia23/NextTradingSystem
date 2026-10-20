using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasOne(x => x.Category).WithMany(x => x.Products)
                    .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.UnitOfMeasure).WithMany(x => x.Products)
                    .HasForeignKey(x => x.UnitOfMeasureId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.Barcode).HasMaxLength(100);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Barcode).IsUnique().HasFilter("\"Barcode\" IS NOT NULL");
        builder.Property(x => x.CostPrice).HasPrecision(15, 2);
        builder.Property(x => x.RetailPrice).HasPrecision(15, 2);
        builder.Property(x => x.WholesalePrice).HasPrecision(15, 2);
        builder.Property(x => x.VipPrice).HasPrecision(15, 2);
        builder.Property(x => x.TaxRate).HasPrecision(5, 2);
        builder.Property(x => x.MinStock).HasPrecision(15, 3);
        builder.Property(x => x.MaxStock).HasPrecision(15, 3);
        builder.HasQueryFilter(x => x.DeletedAt == null);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();
    }
}
