using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
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

public sealed class CreateApplicationUserHandler(IUserCommandService commands, IRoleManagementService roles,
    UserIdentityRules rules, IProfileImageProcessor images, IEntityValidator<UserCreationDto> validator,
    IMapper mapper, IUnitOfWork unitOfWork, ILogger<CreateApplicationUserHandler> logger) : IRequestHandler<CreateApplicationUserCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(CreateApplicationUserCommand request, CancellationToken ct)
    {
        var dto = request.User;
        validator.Validate(dto);
        await rules.BranchAsync(dto.BranchId, null, ct);
        await rules.UniqueAsync(dto.UserName, dto.Email, null, ct);
        var user = mapper.Map<ApplicationUser>(dto);
        user.AvatarUrl = dto.AvatarUrl is null ? null : await images.NormalizeAsync(dto.AvatarUrl, ct);
        var created = await UserOperationTransaction.ExecuteAsync(unitOfWork,
            token => commands.CreateUserAsync(user, dto.Password, token), ct);
        if (!created.Succeeded || string.IsNullOrWhiteSpace(dto.Role)) return created;
        try
        {
            var assigned = await UserOperationTransaction.ExecuteAsync(unitOfWork, async token =>
            {
                if (!await roles.NameExistsAsync(dto.Role.Trim(), cancellationToken: token))
                    return UserOperationResult.Failure([new UserOperationError("RoleNotFound", "The selected role no longer exists.")]);
                return await commands.AddUserToRole(user, dto.Role.Trim(), token);
            }, ct);
            return assigned.Succeeded ? created : RoleWarning();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            try { logger.LogError(error, "Role assignment failed for saved user {UserId}.", user.Id); }
            catch (Exception loggingError) { error.Data["LoggingFailure"] = loggingError; }
            return RoleWarning();
        }
    }
    private static UserOperationResult RoleWarning() => UserOperationResult.Success with
    {
        Warnings = [new UserOperationError("RoleAssignmentFailed", "The user was saved, but role assignment failed. Assign a role from the users list.")]
    };
}
