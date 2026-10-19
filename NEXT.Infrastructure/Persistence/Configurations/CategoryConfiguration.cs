using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasOne(x => x.Parent).WithMany(x => x.Children)
                    .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}
