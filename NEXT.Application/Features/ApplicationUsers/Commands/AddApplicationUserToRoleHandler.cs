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

public sealed class AddApplicationUserToRoleHandler(UserRoleAssignment roles) : IRequestHandler<AddApplicationUserToRoleCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(AddApplicationUserToRoleCommand request, CancellationToken ct) => roles.AssignAsync(request.UserId, request.Role, ct);
}
