using MediatR;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Security;
using NEXT.Domain.Common.QueryModels;

namespace NEXT.Application.Features.Security.Permissions;

public sealed class CreatePermissionHandler(IEntityServices<Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto> service)
    : IRequestHandler<CreatePermissionCommand, PermissionDto>
{
    public Task<PermissionDto> Handle(CreatePermissionCommand request, CancellationToken cancellationToken) => service.Add(request.Data);
}
public sealed class UpdatePermissionHandler(IEntityServices<Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto> service)
    : IRequestHandler<UpdatePermissionCommand, PermissionDto>
{
    public Task<PermissionDto> Handle(UpdatePermissionCommand request, CancellationToken cancellationToken) => service.Update(request.Id, request.Data with { Id = request.Id });
}
public sealed class DeletePermissionHandler(IEntityServices<Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto> service)
    : IRequestHandler<DeletePermissionCommand>
{
    public Task Handle(DeletePermissionCommand request, CancellationToken cancellationToken) => service.Delete(request.Id);
}
public sealed class GetPermissionByIdHandler(IEntityServices<Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto> service)
    : IRequestHandler<GetPermissionByIdQuery, PermissionDto?>
{
    public Task<PermissionDto?> Handle(GetPermissionByIdQuery request, CancellationToken cancellationToken) => service.Details(request.Id);
}
public sealed class GetPermissionsHandler(IEntityServices<Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto> service)
    : IRequestHandler<GetPermissionsQuery, TPaginationQueryModel<PermissionDto>>
{
    public Task<TPaginationQueryModel<PermissionDto>> Handle(GetPermissionsQuery request, CancellationToken cancellationToken) => service.GetAll(request.Pagination);
}
