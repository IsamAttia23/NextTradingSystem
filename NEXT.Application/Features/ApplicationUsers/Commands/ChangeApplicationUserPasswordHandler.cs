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

public sealed class ChangeApplicationUserPasswordHandler(IUserCommandService users, UserDtoReader reader)
    : IRequestHandler<ChangeApplicationUserPasswordCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(ChangeApplicationUserPasswordCommand request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.CurrentPassword)) RequestValidation.Fail("CurrentPassword", "A password is required.");
        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length is < 10 or > 128)
            RequestValidation.Fail("NewPassword", "Password length must be between 10 and 128.");
        return await users.ChangePasswordAsync(await reader.RequireAsync(request.UserId, ct), request.CurrentPassword, request.NewPassword, ct);
    }
}
