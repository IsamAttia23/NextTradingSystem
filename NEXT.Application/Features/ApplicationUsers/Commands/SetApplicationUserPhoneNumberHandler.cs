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

public sealed class SetApplicationUserPhoneNumberHandler(IUserCommandService users, UserDtoReader reader)
    : IRequestHandler<SetApplicationUserPhoneNumberCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(SetApplicationUserPhoneNumberCommand request, CancellationToken ct)
    {
        if (request.PhoneNumber is { Length: > 32 }
            || !string.IsNullOrEmpty(request.PhoneNumber)
            && !new PhoneAttribute().IsValid(request.PhoneNumber))
        {
            RequestValidation.Fail("PhoneNumber", "Enter a valid phone number.");
        }
        return await users.SetPhoneNumberAsync(await reader.RequireAsync(request.UserId, ct), request.PhoneNumber, ct);
    }
}
