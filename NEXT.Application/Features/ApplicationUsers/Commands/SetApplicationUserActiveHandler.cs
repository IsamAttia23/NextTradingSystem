using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Validation;
namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed class SetApplicationUserActiveHandler(IUserManagementService users)
    : IRequestHandler<SetApplicationUserActiveCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(SetApplicationUserActiveCommand request, CancellationToken ct)
    {
        RequestValidation.Identifier(request.UserId);
        if (request.IsActive is null || request.ExpectedIsActive is null)
            RequestValidation.Fail("IsActive", "UserActivationRequired");
        return users.SetActiveAsync(request.UserId, request.IsActive.Value, request.ExpectedIsActive.Value, ct);
    }
}
