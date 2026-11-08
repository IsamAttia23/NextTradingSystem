using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using NEXT.Infrastructure.Localization;

namespace NEXT.Infrastructure.Identity;

public sealed class LocalizedIdentityErrorDescriber(IStringLocalizer<SecurityMessages> text) : IdentityErrorDescriber
{
    private IdentityError Error(string code, params object[] args) => new() { Code = code, Description = text[code, args] };
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), length);
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric));
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit));
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower));
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper));
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), uniqueChars);
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch));
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken));
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure));
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail));
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail));
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName));
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName));
}
