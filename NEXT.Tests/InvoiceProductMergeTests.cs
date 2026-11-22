using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using NEXT.web.Services;
using Xunit;

namespace NEXT.Tests;

public sealed class InvoiceProductMergeTests
{
    [PostgresFact]
    public async Task CreationMergesBeforeRoundingAndRejectsStaleOrExcessDuplicateQuantities()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true, withReceiptRate: true);
        (await f.Db.Warehouses.SingleAsync(item => item.Id == f.Source)).IsDefault = true;
        f.Db.Add(new Stock { ProductId = f.Product, WarehouseId = f.Source, Quantity = 10 });
        await f.Db.SaveChangesAsync();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var token = WorkspaceUiTests.Token(await client.GetStringAsync("/Invoices/Create"));
        var operation = Guid.NewGuid();
        using (var saved = await Post(operation, "0.001", "0.001"))
            Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());
        using (var repeated = await Post(operation, "0.001", "0.001"))
            Assert.True(repeated.IsSuccessStatusCode, await repeated.Content.ReadAsStringAsync());
        var invoice = await f.Db.Invoices.AsNoTracking().Include(item => item.Items).SingleAsync();
        var line = Assert.Single(invoice.Items);
        Assert.Equal(f.Product, line.ProductId);
        Assert.Equal(0.002m, line.Quantity);
        Assert.Equal(4m, line.UnitPrice);
        Assert.Equal(0.01m, line.TotalAmount);
        Assert.Equal(0.01m, invoice.SubTotal);
        Assert.Equal(0.01m, invoice.RemainingAmount);
        using (var stale = await Post(Guid.NewGuid(), "1", "1", "5"))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var shortage = await Post(Guid.NewGuid(), "6", "6"))
            Assert.Equal(HttpStatusCode.BadRequest, shortage.StatusCode);
        using (var excessive = await Post(Guid.NewGuid(), "999999999999.999", "0.001"))
            Assert.Equal(HttpStatusCode.BadRequest, excessive.StatusCode);
        Assert.Equal(1, await f.Db.Invoices.CountAsync());
        Assert.Equal(1, await f.Db.InvoiceItems.CountAsync());
        Assert.Equal(10m, (await f.Db.Stocks.SingleAsync()).Quantity);

        Task<HttpResponseMessage> Post(Guid id, string first, string second, string secondPrice = "4") =>
            WorkspaceUiTests.Post(client, "/Invoices/Create", token,
                ("OperationId", id.ToString()), ("CustomerName", "Merge customer"),
                ("Lines[0].ProductId", f.Product.ToString()), ("Lines[0].Quantity", first), ("Lines[0].QuotedUnitPrice", "4"),
                ("Lines[1].ProductId", f.Product.ToString()), ("Lines[1].Quantity", second), ("Lines[1].QuotedUnitPrice", secondPrice));
    }


}
