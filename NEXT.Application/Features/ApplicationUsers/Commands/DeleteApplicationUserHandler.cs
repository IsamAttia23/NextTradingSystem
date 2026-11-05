using System.ComponentModel.DataAnnotations;
using MapsterMapper;
using MediatR;
using NEXT.Application.Abstractions.Media;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed class DeleteApplicationUserHandler(UserDtoReader reader, IUserCommandService commands,
    ICurrentUserService current, IUserDeletionPolicy policy, IUnitOfWork unitOfWork, IDeletionAuditContext deletionAudit)
    : IRequestHandler<DeleteApplicationUserCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(DeleteApplicationUserCommand request, CancellationToken ct)
    {
        RequestValidation.Identifier(request.UserId, "UserId");
        if (current.UserId == request.UserId)
        {
            throw new UnprocessableEntityException("You cannot delete your own account.");
        }
        var reason = NEXT.Application.Services.DeletionReason.Require(request.Reason);
        return await UserOperationTransaction.ExecuteAsync(unitOfWork, async token =>
        {
            var user = await reader.RequireAsync(request.UserId, token);
            await policy.ValidateAsync(request.UserId, token);
            user.DeletionReason = reason;
            using var audit = deletionAudit.Begin(reason);
            return await commands.DeleteUserAsync(user, token);
        }, ct);
    }
}
