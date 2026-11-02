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

public sealed class AssignUserRoleHandler(UserRoleAssignment roles) : IRequestHandler<AssignUserRoleCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(AssignUserRoleCommand request, CancellationToken ct) => roles.AssignAsync(request.UserId, request.Role, ct);
}
