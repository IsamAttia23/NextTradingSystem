using MapsterMapper;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Security;
using NEXT.Domain.Entities;

namespace NEXT.Application.Services.Security;

/// <summary>Permission CRUD reuses the shared TEntityService implementation.</summary>
public sealed class PermissionEntityService(
    IRepository<Permission> repository,
    IMapper mapper,
    IPaginationServices pagination,
    IUnitOfWork unitOfWork,
    IKeyRepository<Permission, Guid> keyRepository)
    : TEntityService<Permission, Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto>(
        repository, mapper, pagination, unitOfWork, keyRepository);
