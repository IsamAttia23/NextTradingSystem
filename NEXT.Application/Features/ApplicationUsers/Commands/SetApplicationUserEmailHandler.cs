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

public sealed class SetApplicationUserEmailHandler(IUserCommandService users, IUserQueryService queries, UserDtoReader reader)
    : IRequestHandler<SetApplicationUserEmailCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(SetApplicationUserEmailCommand request, CancellationToken ct)
    {
        var email = RequestValidation.Text(request.Email, "Email");
        if (!new EmailAddressAttribute().IsValid(email)) RequestValidation.Fail("Email", "Enter a valid email address.");
        var user = await reader.RequireAsync(request.UserId, ct);
        if (await queries.IsEmailTakenAsync(email, user.Id.ToString(), ct)) RequestValidation.Fail("Email", "This email address is already in use.");
        return await users.SetUserEmailAsync(user, email, ct);
    }
}
