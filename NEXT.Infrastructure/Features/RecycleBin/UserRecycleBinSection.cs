using NEXT.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.RecycleBin;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Security.Services;
using NEXT.Infrastructure.Identity.Security.Storage;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Features.RecycleBin;

public sealed class UserRecycleBinSection(NEXTDbContext db, UserManager<ApplicationUser> users,
    IUnitOfWork unitOfWork, IPaginationServices pagination, ICurrentUserService current,
    IUserDeletionPolicy deletionPolicy, SecurityTransactions transactions) : IRecycleBinSection
{
    public string Name => RecycleBinSectionNames.UserAccounts;

    public Task<TPaginationQueryModel<RecycleBinItemDto<Guid>>> List(PaginationQuery query, CancellationToken ct = default) =>
        StorageErrors.ExecuteAsync(() => pagination.GetPagedList(
            db.Users.IgnoreQueryFilters().AsNoTracking().Where(user => user.DeletedAt != null)
                .OrderByDescending(user => user.DeletedAt).ThenBy(user => user.Id),
            source => source.Select(user => new RecycleBinItemDto<Guid>
            {
                EntityId = user.Id, EntityType = RecycleBinSectionNames.UserAccounts,
                DisplayName = user.FullName + " (" + user.UserName + ")",
                DeletedOn = user.DeletedAt!.Value, DeletionReason = user.DeletionReason
            }), query, ct));

    public Task Restore(Guid id, CancellationToken ct) => unitOfWork.ExecuteInTransactionAsync(async token =>
    {
        await transactions.RunAsync("user:" + id, async () =>
        {
            var user = await RequireDeletedAsync(id, token);
            user.DeletedAt = null;
            user.DeletedBy = null;
            user.DeletionReason = null;
            // Restoration does not reactivate access or resurrect an old session.
            user.IsActive = false;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            UserManagementService.Ensure(await users.UpdateAsync(user));
            return true;
        }, token);
    }, ct);

    public Task Purge(Guid id, CancellationToken ct) => unitOfWork.ExecuteInTransactionAsync(async token =>
    {
        if (current.UserId == id) throw new UnprocessableEntityException("You cannot delete your own account.");
        var user = await RequireDeletedAsync(id, token);
        await deletionPolicy.ValidateAsync(id, token);
        await db.PermanentlyDeleteAsync(user, token);
    }, ct);

    private Task<ApplicationUser> RequireDeletedAsync(Guid id, CancellationToken ct) => StorageErrors.ExecuteAsync(async () =>
        await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(user => user.Id == id && user.DeletedAt != null, ct)
            ?? throw new NotFoundException("Deleted entity was not found."));
}
