using MediatR;
using NEXT.Application.DTOs.Security;
using NEXT.Domain.Common.QueryModels;

namespace NEXT.Application.Features.Security.Permissions;

public sealed record CreatePermissionCommand(PermissionCreationDto Data) : IRequest<PermissionDto>;
public sealed record UpdatePermissionCommand(Guid Id, PermissionUpdateDto Data) : IRequest<PermissionDto>;
public sealed record DeletePermissionCommand(Guid Id) : IRequest;
public sealed record GetPermissionByIdQuery(Guid Id) : IRequest<PermissionDto?>;
public sealed record GetPermissionsQuery(PaginationQuery Pagination) : IRequest<TPaginationQueryModel<PermissionDto>>;
