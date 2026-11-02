using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.ApplicationUsers;

public sealed class UserIdentityRules(IUserQueryService users, IBranchRepository branches)
{
    public async Task UniqueAsync(string name, string email, Guid? excludingId, CancellationToken ct)
    {
        List<(string Field, string Message)> errors = [];
        if (await users.IsUserNameTakenAsync(name, excludingId?.ToString(), ct))
            errors.Add(("UserName", "This username is already in use."));
        if (await users.IsEmailTakenAsync(email, excludingId?.ToString(), ct))
            errors.Add(("Email", "This email address is already in use."));
        ValidationErrors.ThrowIfAny(errors);
    }
    public async Task BranchAsync(Guid branchId, Guid? currentBranchId, CancellationToken ct)
    {
        RequestValidation.Identifier(branchId, "BranchId");
        if (!await branches.AnyAsync(x => x.Id == branchId && (x.IsActive || x.Id == currentBranchId), ct))
            RequestValidation.Fail("BranchId", "Select an active branch.");
    }
}
