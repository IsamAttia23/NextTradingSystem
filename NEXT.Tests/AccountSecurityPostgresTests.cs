using NEXT.Infrastructure.Identity.Entities;
using NEXT.Infrastructure.Identity.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using Npgsql;
using Xunit;

namespace NEXT.Tests;

public sealed class SecurityPostgresFactAttribute : FactAttribute
{
    public SecurityPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEXT_SECURITY_TEST_POSTGRES")))
            Skip = "Set NEXT_SECURITY_TEST_POSTGRES to an isolated, migrated loopback PostgreSQL test database.";
    }
}

public sealed class AccountSecurityPostgresTests
{
    [SecurityPostgresFact]
    public async Task RealTransactionsCommitOtpAndRollBackPasswordChangesWhenGrantConsumptionFails()
    {
        var connection = Environment.GetEnvironmentVariable("NEXT_SECURITY_TEST_POSTGRES")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        Assert.Contains(settings.Host, new[] { "127.0.0.1", "localhost", "::1" });
        var fault = new GrantConsumptionFailure();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: connection,
            configureServices: services => services.ConfigureDbContext<NEXTDbContext>(options => options.AddInterceptors(fault)));
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByNameAsync("ui-reader"))!;
        user.EmailConfirmed = true;
        Assert.True((await users.UpdateAsync(user)).Succeeded);

        var management = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var challengeId = await management.RequestPasswordResetAsync(user.Email!);
        var message = await db.SecurityEmailMessages.SingleAsync();
        var body = scope.ServiceProvider.GetRequiredService<SecuritySecrets>().Decrypt(message.EncryptedBody);
        var code = System.Text.RegularExpressions.Regex.Match(body, @"code is ([0-9]{5})\.").Groups[1].Value;
        var verified = await scope.ServiceProvider.GetRequiredService<IOtpService>()
            .VerifyAsync(challengeId, code, OtpPurpose.PasswordReset);
        Assert.True(verified.Succeeded);
        Assert.NotNull((await db.OtpChallenges.SingleAsync()).UsedAt);

        fault.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => management.ResetPasswordAsync(verified.Grant!, "Updated!12345"));
        db.ChangeTracker.Clear();
        user = (await users.FindByNameAsync("ui-reader"))!;
        Assert.True(await users.CheckPasswordAsync(user, "Test-only!2026"));
        Assert.False(await users.CheckPasswordAsync(user, "Updated!12345"));
        Assert.Null((await db.AccountSecurityGrants.AsNoTracking().SingleAsync()).UsedAt);

        fault.Enabled = false;
        Assert.True((await management.ResetPasswordAsync(verified.Grant!, "Updated!12345")).Succeeded);
        Assert.False((await management.ResetPasswordAsync(verified.Grant!, "Another!12345")).Succeeded);
        Assert.True(await users.CheckPasswordAsync(user, "Updated!12345"));

        var branchId = await db.Branches.Select(x => x.Id).FirstAsync();
        var created = new UserAccount { UserName = "pg-temporary", FullName = "PostgreSQL temporary user",
            Email = "pg-temporary@example.test", BranchId = branchId, IsActive = true };
        Assert.True((await management.CreateWithActivationOtpAsync(created)).Succeeded);
        Assert.True(await db.SecurityEmailMessages.AnyAsync(x => x.UserId == created.Id));
        var stored = await db.Users.SingleAsync(x => x.Id == created.Id);
        Assert.True(stored.MustChangePassword);
        Assert.False(await users.HasPasswordAsync(stored));
        var activationMail = await db.SecurityEmailMessages.SingleAsync(x => x.UserId == created.Id);
        var activationBody = scope.ServiceProvider.GetRequiredService<SecuritySecrets>().Decrypt(activationMail.EncryptedBody);
        var activationCode = System.Text.RegularExpressions.Regex.Match(activationBody, @"\b[0-9]{5}\b").Value;
        Assert.Equal(5, activationCode.Length);
        var activation = await management.PrepareLoginAsync(created.UserName!, activationCode, true);
        Assert.NotNull(activation.SetupToken);
        Assert.Null((await management.PrepareLoginAsync(created.UserName!, activationCode, true)).SetupToken);
        Assert.True((await management.CompleteFirstLoginAsync(activation.SetupToken!, "Activated!12345")).Succeeded);

        fault.FailEmailQueue = true;
        var failedUser = new UserAccount { UserName = "rollback-activation", FullName = "Rollback",
            Email = "rollback@example.test", BranchId = branchId, IsActive = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => management.CreateWithActivationOtpAsync(failedUser));
        db.ChangeTracker.Clear();
        Assert.False(await db.Users.AnyAsync(x => x.UserName == "rollback-activation"));
        Assert.False(await db.OtpChallenges.AnyAsync(x => x.UserId == failedUser.Id));

        using var admin = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(admin, "ui-admin");
        var list = await admin.GetStringAsync("/Users");
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(admin, $"/Users/SetActive/{created.Id}",
            WorkspaceUiTests.Token(list), ("IsActive", "false"), ("ExpectedIsActive", "true"))).StatusCode);
        var deleted = await WorkspaceUiTests.Post(admin, $"/Users/Delete/{created.Id}", WorkspaceUiTests.Token(list),
            ("Reason", "PostgreSQL recycle test"));
        Assert.Equal(System.Net.HttpStatusCode.OK, deleted.StatusCode);
        var recycle = await admin.GetStringAsync("/RecycleBin?section=ApplicationUser");
        Assert.Contains("pg-temporary", recycle);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(admin, "/RecycleBin/Restore",
            WorkspaceUiTests.Token(recycle), ("section", "ApplicationUser"), ("id", created.Id.ToString()))).StatusCode);
        db.ChangeTracker.Clear();
        Assert.False((await db.Users.SingleAsync(x => x.Id == created.Id)).IsActive);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(admin, $"/Users/Delete/{created.Id}",
            WorkspaceUiTests.Token(list), ("Reason", "PostgreSQL purge test"))).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(admin, "/RecycleBin/Purge",
            WorkspaceUiTests.Token(recycle), ("section", "ApplicationUser"), ("id", created.Id.ToString()),
            ("Reason", "PostgreSQL permanent purge"))).StatusCode);
        db.ChangeTracker.Clear();
        Assert.False(await db.Users.IgnoreQueryFilters().AnyAsync(x => x.Id == created.Id));
    }

    private sealed class GrantConsumptionFailure : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public bool FailEmailQueue { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (FailEmailQueue && eventData.Context!.ChangeTracker.Entries<SecurityEmailMessage>()
                .Any(x => x.State == EntityState.Added))
                throw new InvalidOperationException("Simulated email queue persistence failure.");
            if (Enabled && eventData.Context!.ChangeTracker.Entries<AccountSecurityGrant>()
                .Any(x => x.State == EntityState.Modified && x.Entity.UsedAt is not null))
                throw new InvalidOperationException("Simulated grant persistence failure.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
