using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Entities;
namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class AccountOpeningConfiguration : IEntityTypeConfiguration<AccountOpening>
{
    public void Configure(EntityTypeBuilder<AccountOpening> b)
    {
        b.ToTable("AccountOpenings", t => t.HasCheckConstraint("CK_AccountOpenings_Valid",
            "\"Account\" IN (5, 9) AND \"Date\" <> DATE '0001-01-01' AND \"VerifiedBalance\" >= 0 AND \"VerifiedBalance\" <> \"BeforeBalance\" AND length(\"PositionHash\") = 64"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.OperationId).IsUnique();
        b.HasIndex(x => x.Account).IsUnique();
        b.Ignore(x => x.Difference);
        b.Property(x => x.BeforeBalance).HasPrecision(15, 2);
        b.Property(x => x.VerifiedBalance).HasPrecision(15, 2);
        b.Property(x => x.PositionHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.ActorName).HasMaxLength(200).IsRequired();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
