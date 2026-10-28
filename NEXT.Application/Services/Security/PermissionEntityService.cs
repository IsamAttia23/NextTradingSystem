using MapsterMapper;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Application.Validation;

namespace NEXT.Application.Services.Security;

/// <summary>Permission CRUD reuses shared TEntityService while enforcing permission-specific uniqueness.</summary>
public sealed class PermissionEntityService(
    IRepository<Permission> repository,
    IMapper mapper,
    IPaginationServices pagination,
    IUnitOfWork unitOfWork,
    IKeyRepository<Permission, Guid> keyRepository,
    IEntityValidator<PermissionCreationDto> creationValidator,
    IEntityValidator<PermissionUpdateDto> updateValidator,
    NEXT.Application.Abstractions.Context.IDeletionAuditContext deletionAudit)
    : TEntityService<Permission, Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto>(
        repository, mapper, pagination, unitOfWork, keyRepository, deletionAudit)
{
    public override async Task<PermissionDto> Add(PermissionCreationDto creationDTO)
    {
        creationValidator.Validate(creationDTO);
        var code = creationDTO.Code.Trim().ToUpperInvariant();
        if (await Repository.AnyAsync(permission => permission.Code.ToUpper() == code))
            throw new AppValidationException(new Dictionary<string, string[]> {
                [nameof(creationDTO.Code)] = ["This permission code already exists."]
            });
        return await base.Add(creationDTO);
    }

    public override async Task<PermissionDto> Update(Guid id, PermissionUpdateDto updateDto)
    {
        updateValidator.Validate(updateDto);
        var code = updateDto.Code.Trim().ToUpperInvariant();
        if (await Repository.AnyAsync(permission => permission.Id != id && permission.Code.ToUpper() == code))
            throw new AppValidationException(new Dictionary<string, string[]> {
                [nameof(updateDto.Code)] = ["This permission code already exists."]
            });
        return await base.Update(id, updateDto);
    }
}
