using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Accounting;
using NEXT.Application.Features.Products;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using MediatR;
using Xunit;
namespace NEXT.Tests;
public sealed class FinancialControlsTests
{
    [Fact]
    public async Task ClosedPeriodsRejectPostingAndReopeningRequiresAuthorityAndAnAuditReason()
    {
        var actor=new AccountingFoundationTests.TestActor();
        await using var app=await WorkspaceUiTests.StartAsync(configureServices:s=>s.AddSingleton<ICurrentUserService>(actor));
        await using var scope=app.Services.CreateAsyncScope(); var db=await AccountingFoundationTests.Activate(scope.ServiceProvider,actor);
        var controls=scope.ServiceProvider.GetRequiredService<FinancialControls>();
        var id=await controls.CreatePeriodAsync(new() {Name="November",Start=new(2026,11,1),End=new(2026,11,21),Reason="Reviewed dates"},default);
        await Assert.ThrowsAsync<ConflictException>(()=>controls.CreatePeriodAsync(new(){Name="Overlap",Start=new(2026,11,21),End=new(2026,11,30),Reason="Overlap"},default));
        var posting=scope.ServiceProvider.GetRequiredService<IAccountingPostingService>();
        await posting.PostAsync(AccountingFoundationTests.TreasuryOpening(100),default);
        await controls.ChangePeriodAsync(new(){Id=id,Version=1,Close=true,Reason="Month-end review"},default);
        await Assert.ThrowsAsync<ConflictException>(()=>posting.PostAsync(AccountingFoundationTests.TreasuryOpening(10),default));
        await Assert.ThrowsAsync<ConflictException>(()=>controls.ChangePeriodAsync(new(){Id=id,Version=1,Close=false,Reason="Stale"},default));
        actor.UserId=(await db.Users.SingleAsync(u=>u.UserName=="ui-reader")).Id;
        await Assert.ThrowsAsync<ConflictException>(()=>controls.ChangePeriodAsync(new(){Id=id,Version=2,Close=false,Reason="Unauthorized"},default));
        actor.UserId=(await db.Users.SingleAsync(u=>u.UserName=="ui-admin")).Id;
        await controls.ChangePeriodAsync(new(){Id=id,Version=2,Close=false,Reason="Approved correction"},default);
        await posting.PostAsync(AccountingFoundationTests.TreasuryOpening(10),default);
        Assert.Equal(3,await db.FinancialPeriodEvents.CountAsync()); Assert.Equal(2,await db.LedgerEntries.CountAsync());
    }
    [Fact]
    public async Task TransfersPreserveTotalCashRejectOverdrawAndRetriesAndReconciliationUsesExternalEvidence()
    {
        var actor=new AccountingFoundationTests.TestActor();
        await using var app=await WorkspaceUiTests.StartAsync(configureServices:s=>s.AddSingleton<ICurrentUserService>(actor));
        await using var scope=app.Services.CreateAsyncScope(); var db=await AccountingFoundationTests.Activate(scope.ServiceProvider,actor);
        var controls=scope.ServiceProvider.GetRequiredService<FinancialControls>();
        await scope.ServiceProvider.GetRequiredService<IAccountingPostingService>().PostAsync(AccountingFoundationTests.TreasuryOpening(100),default);
        var transfer=new CashTransferInput{OperationId=Guid.NewGuid(),Date=new(2026,11,21),FromAccount=AccountType.Treasury,ToAccount=AccountType.Bank,Amount=40,Reason="Bank deposit"};
        var id=await controls.TransferAsync(transfer,default); Assert.Equal(id,await controls.TransferAsync(transfer,default));
        Assert.Equal(60,(await controls.PositionAsync(AccountType.Treasury,transfer.Date,default)).Balance);
        Assert.Equal(40,(await controls.PositionAsync(AccountType.Bank,transfer.Date,default)).Balance);
        transfer.Amount=41; await Assert.ThrowsAsync<ConflictException>(()=>controls.TransferAsync(transfer,default));
        transfer.OperationId=Guid.NewGuid();transfer.Amount=61;await Assert.ThrowsAsync<ConflictException>(()=>controls.TransferAsync(transfer,default));
        var position=await controls.PositionAsync(AccountType.Bank,transfer.Date,default);
        var reconcile=new CashReconciliationInput{OperationId=Guid.NewGuid(),Account=AccountType.Bank,Date=transfer.Date,PositionHash=position.Hash,ExternalBalance=30,DepositsInTransit=10,Evidence="Bank statement 123",Reason="Deposit in transit"};
        var rid=await controls.ReconcileAsync(reconcile,default);Assert.Equal(rid,await controls.ReconcileAsync(reconcile,default));
        var row=Assert.Single(await controls.ReconciliationsAsync(default));Assert.Equal(0,row.Difference);Assert.False(row.Stale);
        transfer.OperationId=Guid.NewGuid();transfer.Amount=5;await controls.TransferAsync(transfer,default);
        Assert.True(Assert.Single(await controls.ReconciliationsAsync(default)).Stale);
        reconcile.OperationId=Guid.NewGuid();await Assert.ThrowsAsync<ConflictException>(()=>controls.ReconcileAsync(reconcile,default));
        Assert.Equal(2,await db.CashTransfers.CountAsync());Assert.Equal(1,await db.CashReconciliations.CountAsync());
        Assert.Equal(100,await db.AccountTransactions.Where(l=>new[]{AccountType.Treasury,AccountType.Bank}.Contains(l.Account.Type)).SumAsync(l=>l.Debit-l.Credit));
    }
    [Fact]
    public async Task ProductReportUsesComputedPricesAllFilteredRowsAndCategoryNames()
    {
        await using var app=await WorkspaceUiTests.StartAsync();await using var scope=app.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var category=new Category{Name="Test category"};
        db.AddRange(Enumerable.Range(1,15).Select(i=>new Product{Name="Report "+i,Category=category,CostPrice=10,RetailPrice=20,WholesalePrice=10}));
        db.Add(new Product{Name="Excluded",CostPrice=10});await db.SaveChangesAsync();
        var rows=await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProductReportQuery(new ProductQueryModel{CategoryId=category.Id,PageNumber=2,PageSize=1}));
        Assert.Equal(15,rows.Count);Assert.All(rows,p=>{Assert.Equal("Test category",p.CategoryName);Assert.Equal(p.CostPrice*1.2m,p.RetailPrice);Assert.Equal(p.CostPrice*1.1m,p.WholesalePrice);});
        var filtered=await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProductReportQuery(new ProductQueryModel{CategoryId=category.Id,MinRetailPrice=rows[0].RetailPrice+1}));Assert.Empty(filtered);
        using var client=WorkspaceUiTests.Client(app);await WorkspaceUiTests.Login(client,"ui-admin");
        var html=await client.GetStringAsync("/ProductReports/Print?CategoryId="+category.Id+"&PageNumber=2&PageSize=1&culture=en");
        Assert.Contains("Test category",html);Assert.Contains("Report 15",html);Assert.DoesNotContain(">Excluded<",html);
    }
}
