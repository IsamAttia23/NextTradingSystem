# Account security backend

The backend implements five-digit email OTP, administrator-only account unlock,
one-time activation codes, and password recovery. The Razor UI supports Arabic
and English, including right-to-left layout and localized backend responses.

## Responsibilities

- `EmailOtpService` generates codes, checks purpose/expiry/attempts, and issues restricted grants.
- `UserManagementService` creates accounts, handles permanent login locks, and changes passwords through Identity.
- `SecurityEmailService` queues encrypted mail in the same database transaction as the account operation.
- `SecurityEmailDispatcher` sends queued mail through `IEmailTransport`; Resend uses HTTPS and the optional SMTP adapter uses STARTTLS.
- `SecurityTransactions` uses PostgreSQL transaction-scoped advisory locks across application instances.
- `SecurityMaintenance` erases expired encrypted mail and removes expired verification state.
- Controllers forward requests to application handlers and `AccountPresenter`.
- `AccountPresenter` composes MVC responses; `AccountFlowCookies` protects restricted recovery/setup state in HttpOnly cookies.
- Shared password controls and OTP modules also initialize after the site's `next:navigated` event.

## Languages and browser flows

Request localization gives the selected culture cookie priority, followed by the
legacy culture query parameter and the Accept-Language header.
Arabic is the default. `SecurityMessages` resources in Infrastructure translate
Identity password errors, operation failures and email templates. Email subjects
and bodies are rendered before encryption and queuing, so the background worker
preserves the request language. Codes remain five ASCII digits in both languages;
the browser also normalizes Arabic and Persian numerals.

The login page links to `/Account/ForgotPassword`. Recovery continues through
`VerifyOtp`, `SetPassword` and `PasswordChanged`. First login with the five-digit activation
code entered in the password field goes directly to restricted password setup without creating a full session.
The `Locked` page explains that only an administrator can unlock the account.
Invalid OTP shows red borders and a shake; valid OTP shows green outlines and
a continuation button. Both animations respect reduced-motion preferences.
Password fields share show/hide, autocomplete, Caps Lock warnings and confirmation
validation. The server remains authoritative for all security checks.

Browser grants are never put in URLs, page markup or local storage. Cookies are
protected with Data Protection, HttpOnly, SameSite Strict, and Secure under HTTPS.
Configure persistent, protected Data Protection keys when using multiple replicas
or when recovery flows must survive restarts.

## Required deployment configuration

Apply migration `20261107121526_AddAccountSecurity` before deploying this application version.
No migration is applied automatically at application startup.

Set the following environment variables on every application replica:

```text
AccountSecurity__SecretKey=<base64 encoding of at least 32 cryptographically random bytes>
Email__Resend__ApiKey=<Resend API key>
Email__Resend__From=<sender accepted by the provider>
```

Generate the secret locally in PowerShell, then save it as a secret environment variable:

```powershell
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
[Convert]::ToBase64String($bytes)
```

Do not commit this output. Keep the same key across restarts and replicas. Changing
the key invalidates pending OTP fingerprints and prevents decrypting pending mail.
HMAC and mail encryption use separately derived keys. Startup fails if the key is absent or malformed.
API credentials and the security key must not be placed in tracked appsettings files.
Render Free blocks outbound SMTP on ports 25, 465 and 587, so use the HTTPS adapter
on the existing deployment: https://render.com/docs/free . Resend requires an account
and a permitted sender; its testing sender can restrict recipients, while sending
to application users normally requires a verified sending domain. Provider delivery
is not configured automatically by this change.

For other environments an optional SMTP STARTTLS adapter remains available via
`Email__Smtp__Host`, `Email__Smtp__Port` (587), `Email__Smtp__UserName`,
`Email__Smtp__Password`, and `Email__Smtp__From`. Port 465 implicit TLS is not supported.
When both adapters are configured, Resend takes precedence.

The worker does not send messages when neither adapter is configured; expired message
bodies are still erased. Real delivery requires working provider credentials and
a sender accepted by that provider. No real email is sent by the tests.

## Rules

- Codes contain exactly five ASCII digits, including leading zeroes and repeated digits.
- Default OTP lifetime: five minutes; three wrong code attempts revoke the challenge.
- At most three OTP sends per account/purpose in 15 minutes, at least 60 seconds apart.
- Request and verification budgets also apply per client address; rate windows are persisted.
- Resending revokes earlier unused challenges without resetting the account send budget.
- Codes are HMAC-protected, restricted grants are 256-bit opaque values stored only as hashes.
- Grant verification and password changes commit together. A failed password validation does not consume a grant.
- Three failed password logins set `RequiresAdministratorUnlock`; time, OTP and password reset cannot clear it.
- Unlock and temporary-password reissue require a signed-in administrator at the HTTP and service boundaries.
- All cookie-authenticated requests validate the security stamp and current account eligibility.
- Activation uses the same EmailOtpService as password recovery, with EmailVerification purpose.
- New users have no Identity password hash until they choose their permanent password.
- Activation codes expire after 60 minutes by default, using ActivationOtpLifetimeMinutes.
- Change AccountSecurity:ActivationOtpLifetimeMinutes in appsettings/User Secrets, or AccountSecurity__ActivationOtpLifetimeMinutes on Render (1..1440 minutes).
- Recovery OTPs continue using OtpLifetimeMinutes (5 minutes); setup/reset grants use GrantLifetimeMinutes (5 minutes).
- Lifetime changes apply to newly issued codes; existing codes retain their recorded expiry.
- A supplied creation `Password` is ignored; that DTO field remains solely for old-form compatibility.
- Consuming an activation OTP marks its challenge used and issues a purpose-bound setup grant without a full login cookie.
- Successful first-password setup confirms possession of the creation email and returns the user to normal login/MFA.
- A lost or expired activation code/setup grant requires an administrator to reissue an activation OTP.
- Reissue rotates the security stamp and revokes previous codes through the shared OTP service.
- Existing pending users with legacy temporary passwords need an administrator to reissue an activation OTP.
- Existing TemporaryPasswordExpiresAt/UsedAt columns remain for schema compatibility and track activation timestamps.
- Three wrong activation-code attempts also permanently lock the account until an administrator unlocks it.
- Password recovery requires a confirmed email and an active, unlocked account that completed activation.
- Generic recovery responses do not disclose whether the supplied address is registered or eligible.
- Recovery never creates a login session. Identity changes the security stamp, invalidating existing sessions and grants.
- Background email dispatch retries failed sends (up to five attempts), suppresses stale credentials, and erases delivered/expired payloads.
- Security state and encrypted mail are excluded from ordinary audit serialization.

## JSON endpoints

All responses are non-cacheable. POST requests require the existing antiforgery cookie
and a `RequestVerificationToken` header. Obtain the cookie and header value from
`GET /api/account-security/antiforgery`. Never place credentials or grants in URLs.

| Method | Route suffix under `/api/account-security` | Request |
| --- | --- | --- |
| POST | `/first-login` | `{ "userName": "...", "password": "..." }` |
| POST | `/first-login/complete` | `{ "token": "<setupToken>", "newPassword": "..." }` |
| POST | `/password-reset/request` | `{ "email": "..." }` |
| POST | `/password-reset/verify` | `{ "challengeId": "<guid>", "code": "00427" }` |
| POST | `/password-reset/complete` | `{ "token": "<resetToken>", "newPassword": "..." }` |
| POST | `/users/{id}/unlock` | Administrator only |
| POST | `/users/{id}/temporary-password` | Administrator only; pending activation only |

Creation continues through the existing user-creation handler. The frontend phase must
remove the creation password field, handle `RequiresPasswordChange`, and connect these endpoints.
Existing ordinary password/MFA login remains in `AccountAccess`.

## Tests

```powershell
dotnet test NEXT.Tests/NEXT.Tests.csproj
```

The security tests use isolated in-memory databases, an injectable clock, real Identity
password hashing, HTTP cookies/antiforgery, and a fake email transport. They cover
single use, expiry, purpose binding, concurrent verification, lock/unlock, restricted
first login, delivery failures and session revocation.

`AccountSecurityPostgresTests` requires an isolated, fully migrated PostgreSQL database
on loopback, with no existing fixture users. Set `NEXT_SECURITY_TEST_POSTGRES` to its
connection string, then run only that test class. It verifies Npgsql, advisory locks,
the real schema, and rollback of a password change when grant consumption fails.
It does not contact the application database. The agent validated this test using a
temporary PGlite PostgreSQL server; the server multiplexer is not a substitute for
load-testing concurrent transactions on a deployed PostgreSQL server.

## Security folder layout

- Options: configuration models only.
- Contracts: email transport contracts.
- Cryptography: random codes, tokens, HMAC and encrypted message bodies.
- Storage: transaction locks and persistent request limits.
- Email: queue, delivery adapters, worker and cleanup.
- Services: OTP verification, restricted grants and user management.

The old temporary-password HTTP route remains an alias for activation-otp.
Login restores the original WelcomeHeadline/WelcomeDescription resources.
StatusPresenter renders localized access-denied HTML for page navigation and
ProblemDetails for APIs/AJAX form errors. A 403 does not generate a generic server
error notification; genuine 500 responses retain their status.

No additional database migration is required for this revision.
