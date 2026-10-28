using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Security;
using NEXT.web.Models;

namespace NEXT.web.Services;

/// <summary>Loads all permission choices independently of MVC actions.</summary>
public sealed class PermissionOptionsProvider(
    IEntityServices<Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto> permissions)
{
    public async Task<IReadOnlyList<FieldOption>> GetAsync(CancellationToken ct)
    {
        List<FieldOption> options = [];
        var page = 1;
        TPaginationQueryModel<PermissionDto> result;
        do
        {
            ct.ThrowIfCancellationRequested();
            result = await permissions.GetAll(new PaginationQuery { PageNumber = page++, PageSize = 50 });
            options.AddRange(result.Data.Select(x => new FieldOption(x.Id.ToString(), x.Module + " / " + x.Code)));
        } while (page <= result.TotalPages);
        return options;
    }
}
