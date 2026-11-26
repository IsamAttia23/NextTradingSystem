using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Accounting;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using Npgsql;
using Xunit;
namespace NEXT.Tests;
public sealed class FinancialControlsPostgresTests
{
    [PostgresFact]
    public async Task ConcurrentTransfersCannotOverdrawAndPeriodChangesRequireImmutableAudit()
    {
        await using var f=await PostgresInventoryTests.Database.Create(migrate:true);
        var actor=new AccountingFoundationTests.TestActor();
        await using var app=await WorkspaceUiTests.StartAsync(postgresConnection:f.TestConnection,configureServices:s=>s.AddSingleton<ICurrentUserService>(actor));
        Guid period;
        await using(var scope=app.Services.CreateAsyncScope()) {
            await AccountingFoundationTests.Activate(scope.ServiceProvider,actor);
            await scope.ServiceProvider.GetRequiredService<IAccountingPostingService>().PostAsync(AccountingFoundationTests.TreasuryOpening(100),default);
            period=await scope.ServiceProvider.GetRequiredService<FinancialControls>().CreatePeriodAsync(new(){Name="November",Start=new(2026,11,1),End=new(2026,11,21),Reason="Reviewed"},default);
        }
        async Task<bool> Transfer() {
            await using var scope=app.Services.CreateAsyncScope();try {await scope.ServiceProvider.GetRequiredService<FinancialControls>().TransferAsync(new(){OperationId=Guid.NewGuid(),FromAccount=AccountType.Treasury,ToAccount=AccountType.Bank,Amount=70,Date=new(2026,11,21),Reason="Deposit"},default);return true;} catch(ConflictException){return false;}
        }
        var results=await Task.WhenAll(Transfer(),Transfer());Assert.Single(results,x=>x);
        Assert.Equal(1,await f.Db.CashTransfers.CountAsync());Assert.Equal(2,await f.Db.LedgerEntries.CountAsync());
        var missingAudit=await Assert.ThrowsAsync<PostgresException>(()=>f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"FinancialPeriods\" SET \"IsClosed\"=true, \"Version\"=2 WHERE \"Id\"={period}"));Assert.Equal("23514",missingAudit.SqlState);
        await using(var scope=app.Services.CreateAsyncScope()) {
            var controls=scope.ServiceProvider.GetRequiredService<FinancialControls>();
            await controls.ChangePeriodAsync(new(){Id=period,Version=1,Close=true,Reason="Reviewed closing"},default);
            await Assert.ThrowsAsync<ConflictException>(()=>scope.ServiceProvider.GetRequiredService<IAccountingPostingService>().PostAsync(AccountingFoundationTests.TreasuryOpening(1),default));
        }
        var raw=await Assert.ThrowsAsync<PostgresException>(()=>f.Db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "LedgerEntries" ("Id","OperationId","PostingKey","RequestHash","Kind","ReferenceType","ReferenceId","EntryDate","Description","ActorId","ActorName","CreatedAt")
            SELECT 'bbbbbbbb-0000-0000-0000-000000000001','bbbbbbbb-0000-0000-0000-000000000002','3:4:bbbbbbbb000000000000000000000003',"RequestHash",3,4,'bbbbbbbb-0000-0000-0000-000000000003',DATE '2026-11-21','Raw attempt',"ActorId","ActorName",now()
            FROM "LedgerEntries" LIMIT 1
            """));Assert.Equal("23514",raw.SqlState);Assert.Contains("FinancialPeriodNotOpen",raw.MessageText);
        foreach(var sql in new[]{"UPDATE \"CashTransfers\" SET \"Amount\"=1", "DELETE FROM \"FinancialPeriodEvents\"", "DELETE FROM \"FinancialPeriods\""}) {
            var error=await Assert.ThrowsAsync<PostgresException>(()=>f.Db.Database.ExecuteSqlRawAsync(sql));Assert.Equal("23514",error.SqlState);
        }
    }
    [PostgresFact]
    public async Task BankOpeningAndExternalReconciliationAreSourceLinkedAndProtected()
    {
        await using var f=await PostgresInventoryTests.Database.Create(migrate:true);var actor=new AccountingFoundationTests.TestActor();
        await using var app=await WorkspaceUiTests.StartAsync(postgresConnection:f.TestConnection,configureServices:s=>s.AddSingleton<ICurrentUserService>(actor));
        await using var scope=app.Services.CreateAsyncScope();var db=await AccountingFoundationTests.Activate(scope.ServiceProvider,actor);
        var openings=scope.ServiceProvider.GetRequiredService<AccountingOpenings>();var position=await openings.PreviewAsync(AccountType.Bank,default);
        await openings.ApproveAsync(new(){OperationId=Guid.NewGuid(),Account=AccountType.Bank,Date=new(2026,11,21),VerifiedBalance=100,PositionHash=position.Hash,Reason="Bank opening statement"},default);
        var controls=scope.ServiceProvider.GetRequiredService<FinancialControls>();var cash=await controls.PositionAsync(AccountType.Bank,new(2026,11,21),default);
        await controls.ReconcileAsync(new(){OperationId=Guid.NewGuid(),Account=AccountType.Bank,Date=new(2026,11,21),PositionHash=cash.Hash,ExternalBalance=90,DepositsInTransit=10,Evidence="Statement 001",Reason="Deposit pending"},default);
        Assert.Equal(0,Assert.Single(await controls.ReconciliationsAsync(default)).Difference);
        Assert.Equal(1,await f.Db.LedgerEntries.CountAsync()); // Reconciliation itself does not post an adjustment.
        foreach(var sql in new[]{"UPDATE \"CashReconciliations\" SET \"BookBalance\"=1","DELETE FROM \"CashReconciliations\""}) Assert.Equal("23514",(await Assert.ThrowsAsync<PostgresException>(()=>f.Db.Database.ExecuteSqlRawAsync(sql))).SqlState);
        await Assert.ThrowsAsync<PostgresException>(()=>f.Db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "CashReconciliations" ("Id","OperationId","Account","Date","BookBalance","ExternalBalance","DepositsInTransit","OutstandingPayments","PositionHash","Evidence","Reason","ActorId","ActorName","CreatedAt")
            SELECT 'bbbbbbbb-0000-0000-0000-000000000004','bbbbbbbb-0000-0000-0000-000000000005',13,DATE '2026-11-21',999,999,0,0,"PositionHash",'Fake statement','Fake balance',"ActorId","ActorName",now() FROM "CashReconciliations"
            """));
    }
}
