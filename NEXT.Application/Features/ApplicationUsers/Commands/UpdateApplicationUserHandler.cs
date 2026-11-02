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

public sealed class UpdateApplicationUserHandler(IUserCommandService commands, UserDtoReader reader, UserIdentityRules rules,
    IProfileImageProcessor images, IEntityValidator<UserUpdateDto> validator, IMapper mapper)
    : IRequestHandler<UpdateApplicationUserCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(UpdateApplicationUserCommand request, CancellationToken ct)
    {
        validator.Validate(request.User);
        var userId = RequestValidation.Identifier(request.UserId);
        var user = await reader.RequireAsync(userId, ct);
        await rules.BranchAsync(request.User.BranchId, user.BranchId, ct);
        await rules.UniqueAsync(request.User.UserName, request.User.Email, userId, ct);
        var avatar = request.User.AvatarUrl is null ? user.AvatarUrl : await images.NormalizeAsync(request.User.AvatarUrl, ct);
        mapper.Map(request.User, user);
        user.AvatarUrl = avatar;
        return await commands.UpdateUserAsync(user, ct);
    }
}
