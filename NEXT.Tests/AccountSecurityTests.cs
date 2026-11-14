using NEXT.Infrastructure.Identity.Entities;
using NEXT.Infrastructure.Identity.Security.Email;
using NEXT.Infrastructure.Identity.Security.Cryptography;
using NEXT.Infrastructure.Identity.Security.Contracts;
using NEXT.Infrastructure.Identity.Security.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Features.ApplicationUsers.Commands;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using MediatR;
using Xunit;

namespace NEXT.Tests;

public sealed class AccountSecurityTests
{
    [Fact]
    public async Task AdminDisabledPendingActivationShowsLockedPageWithoutIssuingAGrant()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (id, code) = await fixture.CreateActivationUserAsync();
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            fixture.Current.UserId = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .FindByNameAsync("ui-admin"))!.Id;
            Assert.True((await scope.ServiceProvider.GetRequiredService<IUserManagementService>()
                .SetActiveAsync(id, false, true)).Succeeded);
        }
        using var client = WorkspaceUiTests.Client(fixture.App);
        var login = await client.GetStringAsync("/Account/Login");
        var wrong = code == "00000" ? "11111" : "00000";
        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, "/Account/Login", WorkspaceUiTests.Token(login),
            ("UserName", "temporary"), ("Password", wrong))).StatusCode);
        var response = await WorkspaceUiTests.Post(client, "/Account/Login", WorkspaceUiTests.Token(login),
            ("UserName", "temporary"), ("Password", code));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Locked", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
        await using var check = fixture.App.Services.CreateAsyncScope();
        Assert.Empty(await check.ServiceProvider.GetRequiredService<NEXTDbContext>().AccountSecurityGrants.ToListAsync());
    }

    [Theory]
    [InlineData(60, 59, true)]
    [InlineData(60, 60, false)]
    [InlineData(15, 14, true)]
    [InlineData(15, 15, false)]
    public async Task ActivationLifetimeIsConfigurableAndIndependentOfRecovery(int lifetime, int elapsed, bool valid)
    {
        await using var fixture = await SecurityFixture.CreateAsync(services =>
            services.Configure<AccountSecurityOptions>(options => options.ActivationOtpLifetimeMinutes = lifetime));
        var (id, code) = await fixture.CreateActivationUserAsync();
        Assert.Contains($"{lifetime} minutes", Assert.Single(fixture.Transport.Messages).Body);
        var (_, resetChallenge, _) = await fixture.CreateResetChallengeAsync();
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var activation = await db.OtpChallenges.SingleAsync(x => x.UserId == id);
        var recovery = await db.OtpChallenges.SingleAsync(x => x.Id == resetChallenge);
        Assert.Equal(TimeSpan.FromMinutes(lifetime), activation.ExpiresAt - activation.CreatedAt);
        Assert.Equal(TimeSpan.FromMinutes(5), recovery.ExpiresAt - recovery.CreatedAt);
        Assert.Equal(activation.ExpiresAt, (await db.Users.SingleAsync(x => x.Id == id)).TemporaryPasswordExpiresAt);
        fixture.Clock.Advance(TimeSpan.FromMinutes(elapsed));
        var result = await scope.ServiceProvider.GetRequiredService<IUserManagementService>()
            .PrepareLoginAsync("temporary", code, true);
        Assert.Equal(valid, result.SetupToken is not null);
        if (valid)
        {
            var grant = await db.AccountSecurityGrants.SingleAsync(x => x.UserId == id);
            Assert.Equal(TimeSpan.FromMinutes(5), grant.ExpiresAt - fixture.Clock.GetUtcNow());
        }
    }

    [Theory]
    [InlineData("ar", "الوصول غير مسموح", "فريق مترابط. أعمال أفضل.")]
    [InlineData("en", "Access denied", "Connected teams. Better business.")]
    public async Task PermissionFailuresUseLocalized403PagesAndApiProblems(string language, string title, string welcome)
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        using var client = WorkspaceUiTests.Client(fixture.App);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var login = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Login"));
        Assert.Contains(welcome, login);
        await WorkspaceUiTests.Login(client, "ui-reader");
        var denied = await client.GetAsync("/Account/Denied");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var html = WebUtility.HtmlDecode(await denied.Content.ReadAsStringAsync());
        Assert.Contains(title, html);
        Assert.Contains("access-illustration", html);
        Assert.Contains("data-initial-error=\"\"", html);
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        var navigation = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Forbidden, navigation.StatusCode);
        Assert.Contains(title, WebUtility.HtmlDecode(await navigation.Content.ReadAsStringAsync()));
        client.DefaultRequestHeaders.Accept.Clear();
        var api = await client.GetAsync("/Status/403");
        Assert.Equal(HttpStatusCode.Forbidden, api.StatusCode);
        Assert.Equal("application/problem+json", api.Content.Headers.ContentType!.MediaType);
        Assert.Equal(title, (await api.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        client.DefaultRequestHeaders.Remove("X-Requested-With");
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.GetAsync("/Status/500")).StatusCode);
    }

    [Theory]
    [InlineData("ar", "en", "رمز التفعيل")]
    [InlineData("en", "ar", "activation code")]
    public async Task CreatingUserQueuesActivationInTheSelectedCookieLanguage(string selected, string browser, string expected)
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        using var client = WorkspaceUiTests.Client(fixture.App);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(browser);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var login = await client.GetStringAsync("/Account/Login");
        await WorkspaceUiTests.Post(client, "/Culture/Set", WorkspaceUiTests.Token(login), ("culture", selected), ("returnUrl", "/"));
        var form = await client.GetStringAsync("/Users/Create?culture=" + browser);
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var branchId = await db.Branches.Select(x => x.Id).FirstAsync();
        var created = await WorkspaceUiTests.Post(client, "/Users/Create", WorkspaceUiTests.Token(form),
            ("UserName", "language-user"), ("FullName", "Language user"), ("Email", "language@example.test"),
            ("BranchId", branchId.ToString()), ("IsActive", "true"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var mail = await db.SecurityEmailMessages.SingleAsync();
        var body = scope.ServiceProvider.GetRequiredService<SecuritySecrets>().Decrypt(mail.EncryptedBody);
        Assert.Contains(expected, body);
        Assert.Matches(@"\b[0-9]{5}\b", body);
        Assert.True(await scope.ServiceProvider.GetRequiredService<SecurityEmailDispatcher>().DispatchOneAsync(default));
        Assert.Equal(body, Assert.Single(fixture.Transport.Messages).Body);
    }

    [Fact]
    public async Task ActivationOtpUsesTheSharedPurposeExpiryAndAccountLockRules()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (id, code) = await fixture.CreateActivationUserAsync();
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var challenge = await db.OtpChallenges.SingleAsync();
        var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
        Assert.False((await otp.VerifyAsync(challenge.Id, code, OtpPurpose.PasswordReset)).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        for (var n = 0; n < 3; n++)
            Assert.Equal(LoginOutcome.Failed, (await users.PrepareLoginAsync("temporary", "invalid", true)).Outcome);
        var user = await db.Users.SingleAsync(x => x.Id == id);
        Assert.True(user.RequiresAdministratorUnlock);
        Assert.Equal(3, user.AccessFailedCount);
        Assert.False((await otp.VerifyAsync(challenge.Id, code, OtpPurpose.EmailVerification)).Succeeded);
        Assert.NotNull(challenge.RevokedAt);
    }

    [Fact]
    public async Task ConcurrentActivationLoginsConsumeTheCodeOnlyOnce()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (_, code) = await fixture.CreateActivationUserAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var scope = fixture.App.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IUserManagementService>().PrepareLoginAsync("temporary", code, true);
        }));
        Assert.Single(results, result => result.SetupToken is not null);
    }

    [Fact]
    public async Task BrowserFirstLoginRequiresPasswordSetupAndTamperedFlowsAreRejected()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (_, password) = await fixture.CreateActivationUserAsync();
        using var client = WorkspaceUiTests.Client(fixture.App);
        var login = await client.GetStringAsync("/Account/Login");
        var response = await WorkspaceUiTests.Post(client, "/Account/Login", WorkspaceUiTests.Token(login),
            ("UserName", "temporary"), ("Password", password));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("SetPassword", response.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
        var setup = await client.GetStringAsync("/Account/SetPassword");
        Assert.Contains("/js/account-security/password-page.js", setup);
        Assert.DoesNotContain(password, setup);
        var mismatch = await WorkspaceUiTests.Post(client, "/Account/SetPassword", WorkspaceUiTests.Token(setup),
            ("NewPassword", "Permanent!12345"), ("ConfirmPassword", "Other!12345"));
        Assert.Equal(HttpStatusCode.OK, mismatch.StatusCode);
        Assert.DoesNotContain("value=\"Permanent!12345\"", await mismatch.Content.ReadAsStringAsync());
        var changed = await WorkspaceUiTests.Post(client, "/Account/SetPassword", WorkspaceUiTests.Token(setup),
            ("NewPassword", "Permanent!12345"), ("ConfirmPassword", "Permanent!12345"));
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        using var tampered = WorkspaceUiTests.Client(fixture.App);
        tampered.DefaultRequestHeaders.Add("Cookie", "NEXT.PasswordGrant=invalid; NEXT.Recovery=invalid");
        Assert.Equal(HttpStatusCode.Redirect, (await tampered.GetAsync("/Account/SetPassword")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await tampered.GetAsync("/Account/VerifyOtp")).StatusCode);
    }

    [Theory]
    [InlineData("ar", "رمز", "rtl")]
    [InlineData("en", "code", "ltr")]
    public async Task RecoveryPagesApiErrorsAndQueuedEmailsUseTheRequestLanguage(string language, string expected, string direction)
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        using var client = WorkspaceUiTests.Client(fixture.App);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var page = await client.GetStringAsync("/Account/ForgotPassword");
        Assert.Contains($"dir=\"{direction}\"", page);
        Assert.Contains("name=\"Email\"", page);
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            var reader = await db.Users.SingleAsync(x => x.UserName == "ui-reader");
            reader.EmailConfirmed = true;
            await db.SaveChangesAsync();
        }
        var requested = await WorkspaceUiTests.Post(client, "/Account/ForgotPassword", WorkspaceUiTests.Token(page),
            ("Email", "ui-reader@example.test"));
        Assert.Equal(HttpStatusCode.Redirect, requested.StatusCode);
        Assert.EndsWith("VerifyOtp", requested.Headers.Location!.ToString());
        var otpPage = await client.GetStringAsync("/Account/VerifyOtp");
        Assert.Contains("/js/account-security/otp-page.js", otpPage);
        Assert.DoesNotContain("ui-reader@example.test", otpPage);
        Assert.Contains("autocomplete=\"one-time-code\"", otpPage);
        string code;
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            var mail = await db.SecurityEmailMessages.SingleAsync();
            var body = scope.ServiceProvider.GetRequiredService<SecuritySecrets>().Decrypt(mail.EncryptedBody);
            Assert.Contains(expected, body);
            code = Regex.Match(body, @"\b[0-9]{5}\b").Value;
            Assert.Equal(5, code.Length);
            // The worker's culture must not change an already rendered email.
            Assert.True(await scope.ServiceProvider.GetRequiredService<SecurityEmailDispatcher>().DispatchOneAsync(default));
            Assert.Equal(body, Assert.Single(fixture.Transport.Messages).Body);
        }
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        var wrong = code == "00000" ? "11111" : "00000";
        var rejected = await WorkspaceUiTests.Post(client, "/Account/VerifyOtp", WorkspaceUiTests.Token(otpPage), ("Code", wrong));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var error = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("invalid", error.RootElement.GetProperty("kind").GetString());
        Assert.Contains(expected, error.RootElement.GetProperty("message").GetString()!);
        var accepted = await WorkspaceUiTests.Post(client, "/Account/VerifyOtp", WorkspaceUiTests.Token(otpPage), ("Code", code));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.DoesNotContain("resetToken", await accepted.Content.ReadAsStringAsync());
        Assert.Contains(accepted.Headers.GetValues("Set-Cookie"), value => value.StartsWith("NEXT.PasswordGrant=") && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        var passwordPage = await client.GetStringAsync("/Account/SetPassword");
        Assert.Contains("name=\"ConfirmPassword\"", passwordPage);
        var changed = await WorkspaceUiTests.Post(client, "/Account/SetPassword", WorkspaceUiTests.Token(passwordPage),
            ("NewPassword", "NewPermanent!2026"), ("ConfirmPassword", "NewPermanent!2026"));
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.EndsWith("PasswordChanged", changed.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/SetPassword")).StatusCode);
        var login = await client.GetStringAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.Redirect, (await WorkspaceUiTests.Post(client, "/Account/Login", WorkspaceUiTests.Token(login),
            ("UserName", "ui-reader"), ("Password", "NewPermanent!2026"))).StatusCode);
    }

    [Theory]
    [InlineData("ar", "يجب")]
    [InlineData("en", "Passwords")]
    public async Task BackendPasswordPolicyErrorsAreLocalized(string language, string expected)
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (_, password) = await fixture.CreateActivationUserAsync();
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var prepared = await service.PrepareLoginAsync("temporary", password, true);
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = new(language);
            var result = await service.CompleteFirstLoginAsync(prepared.SetupToken!, "weak");
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, error => error.Description.Contains(expected));
        }
        finally { System.Globalization.CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public async Task CreationIgnoresSuppliedPasswordAndQueuesAnEncryptedActivationCode()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateApplicationUserCommand(new UserCreationDto
        {
            UserName = "new-user", FullName = "New user", Email = "new@example.test",
            BranchId = await db.Branches.Select(x => x.Id).FirstAsync(), Password = "Attacker-choice!123"
        }));
        Assert.True(result.Succeeded);
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await manager.FindByNameAsync("new-user"))!;
        Assert.True(user.MustChangePassword);
        Assert.False(user.EmailConfirmed);
        Assert.False(await manager.CheckPasswordAsync(user, "Attacker-choice!123"));
        var queued = await db.SecurityEmailMessages.SingleAsync();
        var plaintext = scope.ServiceProvider.GetRequiredService<SecuritySecrets>().Decrypt(queued.EncryptedBody);
        Assert.NotEqual(plaintext, queued.EncryptedBody);
        Assert.DoesNotContain("Attacker-choice", plaintext);
        Assert.True(await scope.ServiceProvider.GetRequiredService<SecurityEmailDispatcher>().DispatchOneAsync(default));
        var delivered = Assert.Single(fixture.Transport.Messages);
        var password = ActivationCode(delivered.Body);
        Assert.False(await manager.HasPasswordAsync(user));
        Assert.Matches("^[0-9]{5}$", password);
        Assert.Equal("", queued.EncryptedBody);
        Assert.NotNull(queued.SentAt);
        Assert.DoesNotContain(await db.AuditLogs.ToListAsync(), x =>
            x.EntityType == nameof(SecurityEmailMessage) || (x.NewValuesJson?.Contains(password) ?? false));
    }

    [Fact]
    public async Task FirstLoginConsumesActivationOtpAndNeverCreatesAFullSession()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (userId, password) = await fixture.CreateActivationUserAsync();
        using var client = WorkspaceUiTests.Client(fixture.App);
        await AddAntiforgeryAsync(client);
        var response = await client.PostAsJsonAsync("/api/account-security/first-login", new
        {
            userName = "temporary", password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var setupToken = json.RootElement.GetProperty("setupToken").GetString()!;
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/account-security/first-login", new
        {
            userName = "temporary", password
        })).StatusCode);

        await using var scope = fixture.App.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        Assert.False((await service.ResetPasswordAsync(setupToken, "Permanent!12345")).Succeeded);
        Assert.False((await service.CompleteFirstLoginAsync(setupToken, "weak")).Succeeded);
        Assert.True((await service.CompleteFirstLoginAsync(setupToken, "Permanent!12345")).Succeeded);
        Assert.False((await service.CompleteFirstLoginAsync(setupToken, "Another!12345")).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(userId.ToString()))!;
        Assert.False(user.MustChangePassword);
        Assert.True(user.EmailConfirmed);
        Assert.True(await users.CheckPasswordAsync(user, "Permanent!12345"));
        Assert.False(await users.CheckPasswordAsync(user, password));
        var loginForm = await client.GetStringAsync("/Account/Login");
        var loginResponse = await WorkspaceUiTests.Post(client, "/Account/Login", WorkspaceUiTests.Token(loginForm),
            ("UserName", "temporary"), ("Password", "Permanent!12345"));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Profile")).StatusCode);
    }

    [Fact]
    public async Task ThreeFailedLoginsRequireAdministratorUnlockEvenAfterTimePasses()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var target = (await users.FindByNameAsync("ui-reader"))!;
        using var client = WorkspaceUiTests.Client(fixture.App);
        await WorkspaceUiTests.Login(client, "ui-reader");
        for (var n = 0; n < 3; n++)
            Assert.Equal(LoginOutcome.Failed, (await service.PrepareLoginAsync("ui-reader", "Wrong!12345")).Outcome);
        await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().Entry(target).ReloadAsync();
        Assert.True(target.RequiresAdministratorUnlock);
        Assert.Equal(3, target.AccessFailedCount);
        fixture.Clock.Advance(TimeSpan.FromDays(365));
        Assert.Equal(LoginOutcome.AccountLocked, (await service.PrepareLoginAsync("ui-reader", "Test-only!2026")).Outcome);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
        await service.RequestPasswordResetAsync(target.Email!);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().OtpChallenges.ToListAsync());
        fixture.Current.UserId = target.Id;
        await Assert.ThrowsAsync<NEXT.Application.Exceptions.UnprocessableEntityException>(() => service.UnlockUserAsync(target.Id));
        fixture.Current.UserId = (await users.FindByNameAsync("ui-admin"))!.Id;
        Assert.True((await service.UnlockUserAsync(target.Id)).Succeeded);
        Assert.Equal(LoginOutcome.Succeeded, (await service.PrepareLoginAsync("ui-reader", "Test-only!2026")).Outcome);
    }

    [Fact]
    public async Task OtpIsFiveDigitsAndThreeWrongAttemptsInvalidateOnlyTheChallenge()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (userId, challenge, code) = await fixture.CreateResetChallengeAsync();
        Assert.Matches("^[0-9]{5}$", code);
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
        Assert.False((await otp.VerifyAsync(challenge, code, OtpPurpose.EmailVerification)).Succeeded);
        var wrong = code == "00000" ? "00001" : "00000";
        for (var n = 0; n < 3; n++) Assert.False((await otp.VerifyAsync(challenge, wrong, OtpPurpose.PasswordReset)).Succeeded);
        Assert.False((await otp.VerifyAsync(challenge, code, OtpPurpose.PasswordReset)).Succeeded);
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var row = await db.OtpChallenges.FindAsync(challenge);
        Assert.Equal(3, row!.FailedAttempts);
        var user = await db.Users.FindAsync(userId);
        Assert.False(user!.RequiresAdministratorUnlock);
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public async Task ResetGrantIsPurposeBoundSingleUseAndRevokesExistingSessions()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (_, challenge, code) = await fixture.CreateResetChallengeAsync();
        using var client = WorkspaceUiTests.Client(fixture.App);
        await WorkspaceUiTests.Login(client, "ui-reader");
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
        var service = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var verified = await otp.VerifyAsync(challenge, code, OtpPurpose.PasswordReset);
        Assert.True(verified.Succeeded);
        Assert.False((await otp.VerifyAsync(challenge, code, OtpPurpose.PasswordReset)).Succeeded);
        Assert.False((await service.CompleteFirstLoginAsync(verified.Grant!, "Updated!12345")).Succeeded);
        Assert.False((await service.ResetPasswordAsync(verified.Grant!, "weak")).Succeeded);
        Assert.True((await service.ResetPasswordAsync(verified.Grant!, "Updated!12345")).Succeeded);
        Assert.False((await service.ResetPasswordAsync(verified.Grant!, "Another!12345")).Succeeded);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await users.CheckPasswordAsync((await users.FindByNameAsync("ui-reader"))!, "Updated!12345"));
    }

    [Fact]
    public async Task OtpExpiresAndResendingRevokesThePreviousCodeWithoutResettingSendLimits()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (userId, challenge, code) = await fixture.CreateResetChallengeAsync();
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
        await Assert.ThrowsAsync<NEXT.Application.Exceptions.ConflictException>(() => otp.SendAsync(userId, OtpPurpose.PasswordReset));
        fixture.Clock.Advance(TimeSpan.FromSeconds(61));
        var second = await otp.SendAsync(userId, OtpPurpose.PasswordReset);
        Assert.False((await otp.VerifyAsync(challenge, code, OtpPurpose.PasswordReset)).Succeeded);
        fixture.Clock.Advance(TimeSpan.FromSeconds(61));
        await otp.SendAsync(userId, OtpPurpose.PasswordReset);
        fixture.Clock.Advance(TimeSpan.FromSeconds(61));
        await Assert.ThrowsAsync<NEXT.Application.Exceptions.ConflictException>(() => otp.SendAsync(userId, OtpPurpose.PasswordReset));
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.False((await otp.VerifyAsync(second, "00000", OtpPurpose.PasswordReset)).Succeeded);
    }

    [Fact]
    public async Task ConcurrentOtpVerificationIssuesExactlyOneGrant()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (_, challenge, code) = await fixture.CreateResetChallengeAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var scope = fixture.App.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IOtpService>()
                .VerifyAsync(challenge, code, OtpPurpose.PasswordReset);
        }));
        Assert.Single(results, x => x.Succeeded);
    }

    [Fact]
    public async Task ConcurrentLoginFailuresCannotLoseCounts()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
        {
            await using var scope = fixture.App.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IUserManagementService>().PrepareLoginAsync("ui-reader", "Wrong!12345");
        }));
        await using var check = fixture.App.Services.CreateAsyncScope();
        var user = await check.ServiceProvider.GetRequiredService<NEXTDbContext>().Users.SingleAsync(x => x.UserName == "ui-reader");
        Assert.True(user.RequiresAdministratorUnlock);
        Assert.Equal(3, user.AccessFailedCount);
    }

    [Fact]
    public async Task UnknownEmailsHaveTheSameResponseAndAdminEndpointsRequireAuthenticationAndCsrf()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        using var client = WorkspaceUiTests.Client(fixture.App);
        var blocked = await client.PostAsJsonAsync("/api/account-security/password-reset/request", new { email = "unknown@example.test" });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        await AddAntiforgeryAsync(client);
        foreach (var email in new[] { "unknown@example.test", "ui-reader@example.test" })
        {
            var response = await client.PostAsJsonAsync("/api/account-security/password-reset/request", new { email });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.False(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("message").GetString()));
        }
        Assert.Empty(fixture.Transport.Messages);
        var unlock = await client.PostAsJsonAsync($"/api/account-security/users/{Guid.NewGuid()}/unlock", new { });
        Assert.True(unlock.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredSetupGrantRequiresAdminReissueAndReissueInvalidatesOldCredentials()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (userId, password) = await fixture.CreateActivationUserAsync();
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var oldChallenge = await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().OtpChallenges
            .Where(x => x.UserId == userId).Select(x => x.Id).SingleAsync();
        var service = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var first = await service.PrepareLoginAsync("temporary", password, true);
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.False((await service.CompleteFirstLoginAsync(first.SetupToken!, "Updated!12345")).Succeeded);
        fixture.Current.UserId = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync("ui-admin"))!.Id;
        Assert.True((await service.ReissueActivationOtpAsync(userId)).Succeeded);
        Assert.False((await scope.ServiceProvider.GetRequiredService<IOtpService>()
            .VerifyAsync(oldChallenge, password, OtpPurpose.EmailVerification)).Succeeded);
        await scope.ServiceProvider.GetRequiredService<SecurityEmailDispatcher>().DispatchOneAsync(default);
        var nextPassword = ActivationCode(fixture.Transport.Messages.Last().Body);
        Assert.Matches("^[0-9]{5}$", nextPassword);
        Assert.NotNull((await service.PrepareLoginAsync("temporary", nextPassword, true)).SetupToken);
    }

    [Fact]
    public async Task FailedEmailDeliveryRetriesAndExpiredMessagesArePurgedWithoutSending()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await fixture.CreateResetChallengeAsync(dispatch: false);
        fixture.Transport.Fail = true;
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<SecurityEmailDispatcher>();
        Assert.True(await dispatcher.DispatchOneAsync(default));
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var row = await db.SecurityEmailMessages.SingleAsync();
        Assert.Null(row.SentAt);
        Assert.NotEmpty(row.EncryptedBody);
        Assert.False(await dispatcher.DispatchOneAsync(default));
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        fixture.Transport.Fail = false;
        Assert.True(await dispatcher.DispatchOneAsync(default));
        Assert.NotNull(row.CancelledAt);
        Assert.Empty(row.EncryptedBody);
        Assert.Empty(fixture.Transport.Messages);
    }

    private static string ActivationCode(string body) => Regex.Match(body, @"activation code is ([0-9]{5})\.").Groups[1].Value;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThreeFailedSecondFactorAttemptsAlsoRequireAdministratorUnlock(bool recoveryCode)
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        Guid userId;
        await using (var setup = fixture.App.Services.CreateAsyncScope())
        {
            var users = setup.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByNameAsync("ui-reader"))!;
            userId = user.Id;
            user.EmailConfirmed = true;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
            Assert.True((await users.ResetAuthenticatorKeyAsync(user)).Succeeded);
            Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);
            await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 2);
        }
        using var client = WorkspaceUiTests.Client(fixture.App);
        var form = await client.GetStringAsync("/Account/Login");
        var login = await WorkspaceUiTests.Post(client, "/Account/Login", WorkspaceUiTests.Token(form),
            ("UserName", "ui-reader"), ("Password", "Test-only!2026"));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("/Account/Verify", login.Headers.Location!.OriginalString);
        for (var n = 0; n < 3; n++)
        {
            var verification = await client.GetStringAsync("/Account/Verify");
            var response = await WorkspaceUiTests.Post(client, "/Account/Verify", WorkspaceUiTests.Token(verification),
                ("Code", "invalid-code"), ("UseRecoveryCode", recoveryCode.ToString()));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        await using var check = fixture.App.Services.CreateAsyncScope();
        var locked = await check.ServiceProvider.GetRequiredService<NEXTDbContext>().Users.SingleAsync(x => x.Id == userId);
        Assert.True(locked.RequiresAdministratorUnlock);
        Assert.Equal(3, locked.AccessFailedCount);
        Assert.NotNull(locked.LastInvalidLoginAttempt);
        Assert.Null(locked.LastLogin);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
    }

    [Fact]
    public async Task AVerifiedResetGrantCannotUnlockALockedAccount()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (_, challenge, code) = await fixture.CreateResetChallengeAsync();
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
        var service = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var verified = await otp.VerifyAsync(challenge, code, OtpPurpose.PasswordReset);
        for (var n = 0; n < 3; n++) await service.PrepareLoginAsync("ui-reader", "Wrong!12345");
        Assert.False((await service.ResetPasswordAsync(verified.Grant!, "Updated!12345")).Succeeded);
        var user = await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().Users.SingleAsync(x => x.UserName == "ui-reader");
        Assert.True(user.RequiresAdministratorUnlock);
    }

    [Fact]
    public async Task ExpiredActivationOtpsAndExpiredResetGrantsCannotChangePasswords()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (_, password) = await fixture.CreateActivationUserAsync();
        fixture.Clock.Advance(TimeSpan.FromMinutes(61));
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        Assert.Equal(LoginOutcome.Failed, (await service.PrepareLoginAsync("temporary", password, true)).Outcome);
        var (_, challenge, code) = await fixture.CreateResetChallengeAsync();
        var verified = await scope.ServiceProvider.GetRequiredService<IOtpService>().VerifyAsync(challenge, code, OtpPurpose.PasswordReset);
        Assert.True(verified.Succeeded);
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.False((await service.ResetPasswordAsync(verified.Grant!, "Updated!12345")).Succeeded);
    }

    [Fact]
    public async Task EmailChangesInvalidateOutstandingOtpAndCleanupErasesExpiredEncryptedMail()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var (id, challenge, code) = await fixture.CreateResetChallengeAsync();
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == id);
        user.Email = "changed@example.test";
        user.NormalizedEmail = "CHANGED@EXAMPLE.TEST";
        await db.SaveChangesAsync();
        Assert.False((await scope.ServiceProvider.GetRequiredService<IOtpService>()
            .VerifyAsync(challenge, code, OtpPurpose.PasswordReset)).Succeeded);
        var queued = new SecurityEmailMessage
        {
            Id = Guid.NewGuid(), UserId = id, Recipient = user.Email, Subject = "Expired",
            EncryptedBody = "encrypted-expired-payload", CredentialStamp = user.SecurityStamp!,
            CreatedAt = fixture.Clock.GetUtcNow().AddHours(-2), ExpiresAt = fixture.Clock.GetUtcNow().AddHours(-1)
        };
        db.SecurityEmailMessages.Add(queued);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<SecurityMaintenance>().CleanupAsync(default);
        Assert.Empty(queued.EncryptedBody);
        Assert.NotNull(queued.CancelledAt);
    }

    private static async Task AddAntiforgeryAsync(HttpClient client)
    {
        var payload = await client.GetFromJsonAsync<JsonElement>("/api/account-security/antiforgery");
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", payload.GetProperty("token").GetString());
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class TestCurrentUser : ICurrentUserService { public Guid? UserId { get; set; } }
    private sealed class TestTransport : IEmailTransport
    {
        public bool Fail { get; set; }
        public List<(string Recipient, string Body)> Messages { get; } = [];
        public Task SendAsync(string recipient, string subject, string body, CancellationToken ct)
        {
            if (Fail) throw new InvalidOperationException("Simulated mail delivery failure.");
            Messages.Add((recipient, body));
            return Task.CompletedTask;
        }
    }

    private sealed class SecurityFixture(WebApplication app, TestClock clock, TestTransport transport,
        TestCurrentUser current) : IAsyncDisposable
    {
        public WebApplication App { get; } = app;
        public TestClock Clock { get; } = clock;
        public TestTransport Transport { get; } = transport;
        public TestCurrentUser Current { get; } = current;

        public static async Task<SecurityFixture> CreateAsync(Action<IServiceCollection>? configure = null)
        {
            var clock = new TestClock(); var transport = new TestTransport(); var current = new TestCurrentUser();
            var app = await WorkspaceUiTests.StartAsync(configureServices: services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<IEmailTransport>(transport);
                services.AddSingleton<ICurrentUserService>(current);
                configure?.Invoke(services);
            });
            return new(app, clock, transport, current);
        }

        public async Task<(Guid UserId, string Password)> CreateActivationUserAsync()
        {
            await using var scope = App.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            var user = new UserAccount { Id = Guid.NewGuid(), UserName = "temporary", FullName = "Temporary",
                Email = "temporary@example.test", IsActive = true, BranchId = await db.Branches.Select(x => x.Id).FirstAsync() };
            Assert.True((await scope.ServiceProvider.GetRequiredService<IUserManagementService>().CreateWithActivationOtpAsync(user)).Succeeded);
            Assert.True(await scope.ServiceProvider.GetRequiredService<SecurityEmailDispatcher>().DispatchOneAsync(default));
            return (user.Id, ActivationCode(Transport.Messages.Last().Body));
        }

        public async Task<(Guid UserId, Guid Challenge, string Code)> CreateResetChallengeAsync(bool dispatch = true)
        {
            await using var scope = App.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            var user = await db.Users.SingleAsync(x => x.UserName == "ui-reader");
            user.EmailConfirmed = true;
            await db.SaveChangesAsync();
            var challenge = await scope.ServiceProvider.GetRequiredService<IUserManagementService>().RequestPasswordResetAsync(user.Email!);
            Assert.True(await db.OtpChallenges.AnyAsync(x => x.Id == challenge));
            if (!dispatch) return (user.Id, challenge, "");
            Assert.True(await scope.ServiceProvider.GetRequiredService<SecurityEmailDispatcher>().DispatchOneAsync(default));
            var code = Regex.Match(Transport.Messages.Last().Body, @"code is ([0-9]{5})\.").Groups[1].Value;
            return (user.Id, challenge, code);
        }

        public ValueTask DisposeAsync() => App.DisposeAsync();
    }
}
