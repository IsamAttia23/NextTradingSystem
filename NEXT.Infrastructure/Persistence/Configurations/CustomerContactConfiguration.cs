using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class CustomerContactConfiguration : IEntityTypeConfiguration<CustomerContact>
{
    public void Configure(EntityTypeBuilder<CustomerContact> builder)
    {
        builder.ToTable("CustomerContacts");
        builder.HasOne(x => x.Customer).WithMany(x => x.Contacts)
                    .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}
