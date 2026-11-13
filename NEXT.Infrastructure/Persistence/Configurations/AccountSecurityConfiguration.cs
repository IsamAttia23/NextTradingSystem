using NEXT.Infrastructure.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.Configurations;

public sealed class AccountSecurityConfiguration : IEntityTypeConfiguration<OtpChallenge>,
    IEntityTypeConfiguration<AccountSecurityGrant>, IEntityTypeConfiguration<SecurityRequestWindow>,
    IEntityTypeConfiguration<SecurityEmailMessage>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> b)
    {
        b.ToTable("OtpChallenges", "security");
        b.HasKey(x => x.Id);
        b.Property(x => x.SecretDigest).HasMaxLength(64);
        b.Property(x => x.CredentialStamp).HasMaxLength(256);
        b.Property(x => x.Email).HasMaxLength(256);
        b.HasIndex(x => new { x.UserId, x.Purpose, x.CreatedAt });
        b.HasIndex(x => x.ExpiresAt);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<AccountSecurityGrant> b)
    {
        b.ToTable("AccountSecurityGrants", "security");
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(64);
        b.Property(x => x.Purpose).HasMaxLength(32);
        b.Property(x => x.CredentialStamp).HasMaxLength(256);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.ExpiresAt);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<SecurityRequestWindow> b)
    {
        b.ToTable("SecurityRequestWindows", "security");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(160);
    }

    public void Configure(EntityTypeBuilder<SecurityEmailMessage> b)
    {
        b.ToTable("SecurityEmailMessages", "security");
        b.HasKey(x => x.Id);
        b.Property(x => x.Recipient).HasMaxLength(256);
        b.Property(x => x.Subject).HasMaxLength(200);
        b.Property(x => x.CredentialStamp).HasMaxLength(256);
        b.HasIndex(x => new { x.SentAt, x.CancelledAt, x.NextAttemptAt });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
