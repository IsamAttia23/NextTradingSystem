using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Invoices;
using NEXT.Application.DTOs.Inventory;
using NEXT.Application.Features.Invoices;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;
using NEXT.Infrastructure.Persistence;
using Xunit;
namespace NEXT.Tests;
public sealed class TrialSalesTests
{
    [Fact]
    public void ShortageValuationAndReplenishmentPreserveCostAndRounding()
    {
        Assert.Equal(50, TrialInventoryValuation.Issue(0,0,5,10));
        Assert.Equal(6, TrialInventoryValuation.ReceiptAdjustment(-5,-50,3,12));
        Assert.Equal(4, TrialInventoryValuation.ReceiptAdjustment(-2,-20,4,12));
        Assert.Equal(3.02m, TrialInventoryValuation.Issue(3,3.02m,3,1.01m));
        Assert.Equal(4.03m, TrialInventoryValuation.Issue(3,3.02m,4,1.01m));
    }
    [PostgresFact]
    public async Task NeverStockedProductCanBeDeliveredAndReplenishmentAdjustsEstimatedCostExactly()
    {
        await using var f=await PostgresInventoryTests.Database.Create(migrate:true);
        var actor=new AccountingFoundationTests.TestActor();
        await using var app=await WorkspaceUiTests.StartAsync(postgresConnection:f.TestConnection,configureServices:s=>s.AddSingleton<ICurrentUserService>(actor));
        app.Configuration["Inventory:AllowInvoiceOverselling"]="true";
        await using var scope=app.Services.CreateAsyncScope();var db=await AccountingFoundationTests.Activate(scope.ServiceProvider,actor);
        var warehouse=await db.Warehouses.AsTracking().SingleAsync(w=>w.Id==f.Source);warehouse.IsDefault=true;
        var p=await db.Products.AsTracking().SingleAsync(p=>p.Id==f.Product);p.CostPrice=10;p.RetailPrice=0;
        var customer=new Customer{Name="Trial credit customer",Phone="+249123456789"};db.Add(customer);await db.SaveChangesAsync();
        var created=await scope.ServiceProvider.GetRequiredService<InvoiceCreation>().CreateAsync(new(){OperationId=Guid.NewGuid(),CustomerId=customer.Id,CustomerName=customer.Name,Lines=[new(){ProductId=p.Id,Quantity=5,QuotedUnitPrice=10}]},default);
        var invoice=await db.Invoices.AsNoTracking().SingleAsync(i=>i.Id==created.Id);
        await scope.ServiceProvider.GetRequiredService<InvoiceConfirmation>().ConfirmAsync(invoice.Id,new(){OperationId=Guid.NewGuid(),RowVersion=invoice.RowVersion},default);
        invoice=await db.Invoices.AsNoTracking().SingleAsync(i=>i.Id==created.Id);
        await scope.ServiceProvider.GetRequiredService<InvoiceFulfillment>().ExecuteAsync(invoice.Id,new(){OperationId=Guid.NewGuid(),RowVersion=invoice.RowVersion,TargetStatus=InvoiceStatus.Delivered},default);
        var stock=await db.Stocks.AsNoTracking().SingleAsync();Assert.Equal(-5,stock.Quantity);Assert.Equal(-50,stock.TotalCost);Assert.Equal(0,stock.ReservedQuantity);
        Assert.Equal(50,await Net(db,AccountType.CostOfSales));Assert.Equal(-50,await Net(db,AccountType.Inventory));
        p=await db.Products.AsTracking().SingleAsync(p=>p.Id==f.Product);p.CostPrice=12;await db.SaveChangesAsync();
        var inventory=scope.ServiceProvider.GetRequiredService<IInventoryService>();
        await inventory.ReceiveAsync(new(){OperationId=Guid.NewGuid(),ProductId=p.Id,WarehouseId=f.Source,SupplierId=f.Supplier,Quantity=3});
        stock=await db.Stocks.AsNoTracking().SingleAsync();Assert.Equal(-2,stock.Quantity);Assert.Equal(-20,stock.TotalCost);Assert.Equal(56,await Net(db,AccountType.CostOfSales));
        await inventory.ReceiveAsync(new(){OperationId=Guid.NewGuid(),ProductId=p.Id,WarehouseId=f.Source,SupplierId=f.Supplier,Quantity=4});
        stock=await db.Stocks.AsNoTracking().SingleAsync();Assert.Equal(2,stock.Quantity);Assert.Equal(24,stock.TotalCost);Assert.Equal(24,await Net(db,AccountType.Inventory));Assert.Equal(60,await Net(db,AccountType.CostOfSales));
        Assert.Equal(10,await db.StockMovements.SumAsync(m=>m.CostAdjustment));
    }
    private static Task<decimal> Net(NEXTDbContext db,AccountType a)=>db.AccountTransactions.Where(l=>l.AccountId==Account.IdFor(a)).SumAsync(l=>l.Debit-l.Credit);
}
