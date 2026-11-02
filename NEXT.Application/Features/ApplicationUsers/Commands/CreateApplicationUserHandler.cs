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

public sealed class CreateApplicationUserHandler(IUserCommandService commands, IRoleManagementService roles,
    UserIdentityRules rules, IProfileImageProcessor images, IEntityValidator<UserCreationDto> validator,
    IMapper mapper, IUnitOfWork unitOfWork) : IRequestHandler<CreateApplicationUserCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(CreateApplicationUserCommand request, CancellationToken ct)
    {
        var dto = request.User;
        validator.Validate(dto);
        await rules.BranchAsync(dto.BranchId, null, ct);
        await rules.UniqueAsync(dto.UserName, dto.Email, null, ct);
        if (!string.IsNullOrWhiteSpace(dto.Role) && !await roles.NameExistsAsync(dto.Role.Trim(), cancellationToken: ct))
            RequestValidation.Fail("Role", "Select an existing role.");
        var user = mapper.Map<ApplicationUser>(dto);
        user.AvatarUrl = dto.AvatarUrl is null ? null : images.Normalize(dto.AvatarUrl);
        return await UserOperationTransaction.ExecuteAsync(unitOfWork, async token =>
        {
            if (!string.IsNullOrWhiteSpace(dto.Role) && !await roles.NameExistsAsync(dto.Role.Trim(), cancellationToken: token))
                RequestValidation.Fail("Role", "Select an existing role.");
            UserOperationTransaction.RequireSuccess(await commands.CreateUserAsync(user, dto.Password, token));
            if (!string.IsNullOrWhiteSpace(dto.Role))
                UserOperationTransaction.RequireSuccess(await commands.AddUserToRole(user, dto.Role.Trim(), token));
            return UserOperationResult.Success;
        }, ct);
    }
}
