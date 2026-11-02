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

public sealed class SetApplicationUserNameHandler(IUserCommandService users, IUserQueryService queries, UserDtoReader reader)
    : IRequestHandler<SetApplicationUserNameCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(SetApplicationUserNameCommand request, CancellationToken ct)
    {
        var name = RequestValidation.Text(request.UserName, "UserName");
        var user = await reader.RequireAsync(request.UserId, ct);
        if (await queries.IsUserNameTakenAsync(name, user.Id.ToString(), ct)) RequestValidation.Fail("UserName", "This username is already in use.");
        return await users.SetUserNameAsync(user, name, ct);
    }
}
