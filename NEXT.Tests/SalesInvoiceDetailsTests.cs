using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using NEXT.Infrastructure.Persistence.Repositories.Entities;
using Xunit;

namespace NEXT.Tests;

public sealed class SalesInvoiceDetailsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DetailsRetainLinesAndProductsWithoutChangingNormalProductVisibility(bool deleted)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var user = await db.Users.FirstAsync(x => x.UserName == "ui-admin");
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(), Name = "Invoice test warehouse", Code = "INVOICE-TEST",
            BranchId = user.BranchId, IsActive = true
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), Name = "Historical invoice product", IsActive = true,
            DeletedAt = deleted ? DateTimeOffset.UtcNow : null
        };
        var invoice = new SalesInvoice
        {
            Id = Guid.NewGuid(), InvoiceNumber = "INVOICE-DETAILS-TEST",
            UserId = user.Id, WarehouseId = warehouse.Id, TotalAmount = 60,
            Items = new List<SalesInvoiceItem>
            {
                new() { Id = Guid.NewGuid(), ProductId = product.Id, Quantity = 2, UnitPrice = 10, TotalAmount = 20 },
                new() { Id = Guid.NewGuid(), ProductId = product.Id, Quantity = 4, UnitPrice = 10, TotalAmount = 40 }
            }
        };
        db.AddRange(warehouse, product, invoice);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var details = await new SalesInvoiceRepository(db).GetDetailsAsync(invoice.Id);

        Assert.NotNull(details);
        Assert.Equal(user.Id, details.User.Id);
        Assert.Equal(user.FullName, details.User.FullName);
        Assert.Equal(2, details.Items.Count);
        Assert.Equal(60m, details.Items.Sum(x => x.TotalAmount));
        Assert.All(details.Items, item =>
        {
            Assert.NotNull(item.Product);
            Assert.Equal(product.Id, item.Product.Id);
            Assert.Equal(product.Name, item.Product.Name);
            Assert.Equal(deleted, item.Product.DeletedAt is not null);
        });
        Assert.Same(details.Items.First().Product, details.Items.Last().Product);
        Assert.Equal(!deleted, await db.Products.AnyAsync(x => x.Id == product.Id));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task MissingInvoiceReturnsNullAndCancellationIsPreserved()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var repository = new SalesInvoiceRepository(scope.ServiceProvider.GetRequiredService<NEXTDbContext>());

        Assert.Null(await repository.GetDetailsAsync(Guid.NewGuid()));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetDetailsAsync(Guid.NewGuid(), canceled.Token));
    }
}
