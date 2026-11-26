using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Features.Accounting;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Entities;
using NEXT.Infrastructure.Persistence;
using NEXT.Infrastructure.Persistence.Repositories;

namespace NEXT.Tests;

// Uses the production posting service and validator; only the authenticated user lookup is supplied by the fixture.
internal static class InventoryTestServices
{
    public static IAccountingPostingService Posting(NEXTDbContext db, Guid branch)
    {
        var user = db.Users.FirstOrDefault();
        if (user is null)
        {
            user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "inventory-test", FullName = "Inventory operator", BranchId = branch, IsActive = true };
            db.Users.Add(user);
            db.SaveChanges();
        }
        var actor = new Actor(user.Id);
        var users = DispatchProxy.Create<IUserQueryService, Lookup>();
        ((Lookup)(object)users).User = new UserAccount { Id = user.Id, FullName = user.FullName, IsActive = true };
        var store = new AccountingStore(db);
        return new AccountingPostingService(store, new UnitOfWork(db), actor, users, TimeProvider.System,
            new AccountingPostingValidator(store, TimeProvider.System, new FinancialControlStore(db)), new FinancialControlStore(db));
    }
    private sealed record Actor(Guid Id) : ICurrentUserService { public Guid? UserId => Id; }
    public class Lookup : DispatchProxy
    {
        public UserAccount User { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IUserQueryService.FindUserAsync) && !targetMethod.IsGenericMethod
            ? Task.FromResult<UserAccount?>(User) : throw new NotSupportedException();
    }
}
