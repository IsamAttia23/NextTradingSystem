using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Infrastructure.Persistence.Configurations
{
    public class TagConfiguration : IEntityTypeConfiguration<Tag>
    {
        public void Configure(EntityTypeBuilder<Tag> builder)
        {
           builder.ToTable("Tags");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Name)
             .IsRequired()
             .HasMaxLength(100);

            builder.Property(t => t.Color)
             .HasMaxLength(20);

            builder.HasMany(t => t.Customers)
             .WithOne(c => c.Tag);

            builder.HasMany(t => t.Suppliers)
             .WithOne(s => s.Tag);
        }
    }
}
