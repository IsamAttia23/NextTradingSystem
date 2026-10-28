using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NEXT.Application.Abstractions.Context;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Features.RecycleBin;
using NEXT.Infrastructure.Persistence;
using NEXT.Infrastructure.Persistence.Repositories;
using NEXT.Infrastructure.Persistence.SaveChanges;
using Xunit;

namespace NEXT.Tests;

public class PersistenceRegressionTests
{
    [Fact]
    public async Task KeyLookupDoesNotReturnTrackedSoftDeletedEntity()
    {
        await using var db = CreateContext();
        var tag = new Tag { Name = "Deleted", Color = "blue" };
        db.Add(tag);
        await db.SaveChangesAsync();
        var repository = new KeyRepository<Tag, Guid>(db);
        await repository.DeleteAsync(tag);
        await db.SaveChangesAsync();
        Assert.Null(await repository.GetByIdAsync(tag.Id));
        Assert.Null(await repository.GetByKeyAsync([tag.Id]));
    }

    private readonly InMemoryDatabaseRoot root = new();
    private NEXTDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NEXTDbContext>().UseInMemoryDatabase("regression", root).Options;
        var serializer = new AuditValueSerializer();
        var user = new TestContext();
        return new NEXTDbContext(options,
            new TrackedChangesProcessor(user, serializer, new SoftDeleteHandler(serializer), new AuditFieldsHandler()),
            new AuditEntryFactory(user, user, serializer));
    }

    [Fact]
    public async Task SoftDeleteRestoreAndPurgePersistWithAudit()
    {
        await using var db = CreateContext();
        var tag = new Tag { Name = "Retail", Color = "green" };
        db.Add(tag);
        await db.SaveChangesAsync();
        await new KeyRepository<Tag, Guid>(db).DeleteAsync(tag);
        await db.SaveChangesAsync();
        Assert.Empty(await db.Tags().ToListAsync());
        Assert.Single(await db.RecycleBinEntries.ToListAsync());
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == AuditAction.Delete));
        var recycle = new RecycleBinService<Tag, Guid>(db, new PaginationServices(), []);
        var page = await recycle.List(new());
        Assert.Equal(tag.Id, Assert.Single(page.Data).EntityId);
        await recycle.Restore(tag.Id);
        Assert.Null(tag.DeletedBy);
        Assert.Single(await db.Tags().ToListAsync());
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == AuditAction.Restore));
        await new KeyRepository<Tag, Guid>(db).DeleteAsync(tag);
        await db.SaveChangesAsync();
        await recycle.HardDelete(tag.Id);
        Assert.Empty(await db.Set<Tag>().IgnoreQueryFilters().ToListAsync());
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == AuditAction.Purge));
    }

    [Fact]
    public async Task ConcurrentUpdatesRejectStaleOriginalToken()
    {
        await using var first = CreateContext();
        var product = new Product { Name = "Item" };
        first.Add(product);
        await first.SaveChangesAsync();
        Assert.Equal(16, product.RowVersion.Length);
        var original = product.RowVersion.ToArray();
        await using var second = CreateContext();
        var stale = await second.Products.SingleAsync();
        product.Name = "First edit";
        await first.SaveChangesAsync();
        Assert.False(original.SequenceEqual(product.RowVersion));
        stale.Name = "Stale edit";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task PurgeDoesNotDeleteActiveEntity()
    {
        await using var db = CreateContext();
        var tag = new Tag { Name = "Active", Color = "blue" };
        db.Add(tag);
        await db.SaveChangesAsync();
        var recycle = new RecycleBinService<Tag, Guid>(db, new PaginationServices(), []);
        await Assert.ThrowsAsync<NEXT.Application.Exceptions.NotFoundException>(() => recycle.HardDelete(tag.Id));
        Assert.Single(await db.Set<Tag>().ToListAsync());
    }

    private sealed class TestContext : ICurrentUserService, IRequestMetadataService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? IpAddress => null;
        public string? UserAgent => null;
    }
}

internal static class TestQueries
{
    internal static IQueryable<Tag> Tags(this NEXTDbContext db) => db.Set<Tag>();
}
