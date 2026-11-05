using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NEXT.Infrastructure.Persistence.Transactions;
using System.Linq.Expressions;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.SaveChanges;
using NEXT.Domain.Common.Entities;

namespace NEXT.Infrastructure.Persistence;

public class NEXTDbContext(DbContextOptions<NEXTDbContext> options, TrackedChangesProcessor trackedChangesProcessor,
    AuditEntryFactory auditEntryFactory)
    : IdentityDbContext<ApplicationUser, Role, Guid, IdentityUserClaim<Guid>, UserRole,
        IdentityUserLogin<Guid>, IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>(options)
{
    private readonly HashSet<object> _permanentDeletions = new(ReferenceEqualityComparer.Instance);
    internal bool IsPermanentDeletion(object entity) => _permanentDeletions.Contains(entity);

    internal async Task PermanentlyDeleteAsync<TEntity>(TEntity entity, CancellationToken ct) where TEntity : class
    {
        _permanentDeletions.Add(entity);
        try
        {
            Remove(entity);
            await SaveChangesAsync(ct);
        }
        finally
        {
            _permanentDeletions.Remove(entity);
        }
    }

    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<UnitOfMeasure> Units => Set<UnitOfMeasure>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<ProductBatch> Batches => Set<ProductBatch>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RecycleBinEntry> RecycleBinEntries => Set<RecycleBinEntry>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // يجب استدعاؤه أولًا لتهيئة مفاتيح وجداول Identity
        base.OnModelCreating(builder);

        // تحميل إعدادات كيانات المشروع
        builder.ApplyConfigurationsFromAssembly(
            typeof(NEXTDbContext).Assembly);

        // جداول ASP.NET Core Identity
        builder.Entity<IdentityUserClaim<Guid>>()
            .ToTable("UserClaims", "security");
        builder.Entity<IdentityUserLogin<Guid>>()
                    .ToTable("UserLogins", "security");
        builder.Entity<IdentityUserToken<Guid>>()
            .ToTable("UserTokens", "security");
        builder.Entity<IdentityRoleClaim<Guid>>()
                    .ToTable("RoleClaims", "security");

        // تطبيق فلاتر الحذف المنطقي
        ApplySoftDeleteQueryFilters(builder);
    }

    private static void ApplySoftDeleteQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var entityClrType = entityType.ClrType;
            var isSoftDeletable = typeof(IsDeletableEntity<Guid>).IsAssignableFrom(entityClrType);
            var isUser = entityClrType == typeof(ApplicationUser);
            if (!isSoftDeletable && !isUser)
            {
                continue;
            }
            var parameter = Expression.Parameter(entityClrType, "entity");
            var deletedAt = Expression.Property(parameter, nameof(IsDeletableEntity<Guid>.DeletedAt));
            var isNotDeleted = Expression.Equal(
                deletedAt,
                Expression.Constant(null, typeof(DateTimeOffset?)));
            var filter = Expression.Lambda(isNotDeleted, parameter);
            builder.Entity(entityClrType).HasQueryFilter(filter);
        }
    }

    internal Exception? TransactionFailure { get; set; }
    internal void EnsureTransactionUsable()
    {
        if (TransactionFailure is not null)
        {
            throw new InvalidOperationException("The transaction cannot continue after a failed rollback.", TransactionFailure);
        }
    }

    public override int SaveChanges() => SaveChanges(true);
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureTransactionUsable();
        var state = new SaveChangesStatePreserver(this);
        var before = state.Capture();
        PersistenceTransaction? transaction = null;
        Exception? failure = null;
        try
        {
            transaction = PersistenceTransaction.Begin(this);
            var pending = trackedChangesProcessor.Prepare(this);
            var result = base.SaveChanges(false);
            var written = state.Capture(); // Includes generated keys and new concurrency tokens.
            state.MarkWritten();
            if (auditEntryFactory.AddAuditRows(this, pending) > 0)
            {
                result += base.SaveChanges(false);
            }
            transaction.Complete();
            FinishSave(state, written, acceptAllChangesOnSuccess);
            return result;
        }
        catch (Exception error)
        {
            failure = error;
            if (transaction?.Rollback(error) == false)
            {
                TransactionFailure = error;
            }
            RestoreAfterFailure(state, before, error);
            StorageErrors.ThrowTranslated(error);
            throw;
        }
        finally
        {
            transaction?.Dispose(failure);
        }
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => SaveChangesAsync(true, cancellationToken);
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureTransactionUsable();
        cancellationToken.ThrowIfCancellationRequested();
        var state = new SaveChangesStatePreserver(this);
        var before = state.Capture();
        PersistenceTransaction? transaction = null;
        Exception? failure = null;
        try
        {
            transaction = await PersistenceTransaction.BeginAsync(this, cancellationToken);
            var pending = trackedChangesProcessor.Prepare(this);
            var result = await base.SaveChangesAsync(false, cancellationToken);
            var written = state.Capture();
            state.MarkWritten();
            if (auditEntryFactory.AddAuditRows(this, pending) > 0)
            {
                result += await base.SaveChangesAsync(false, cancellationToken);
            }
            await transaction.CompleteAsync(cancellationToken);
            FinishSave(state, written, acceptAllChangesOnSuccess);
            return result;
        }
        catch (Exception error)
        {
            failure = error;
            if (transaction is not null && !await transaction.RollbackAsync(error))
            {
                TransactionFailure = error;
            }
            RestoreAfterFailure(state, before, error);
            StorageErrors.ThrowTranslated(error);
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync(failure);
            }
        }
    }
    private void FinishSave(SaveChangesStatePreserver state, IReadOnlyList<EntryStateSnapshot> written, bool accept)
    {
        if (accept)
        {
            ChangeTracker.AcceptAllChanges();
        }
        else
        {
            state.Restore(written, removeNewEntries: false);
        }
    }
    private void RestoreAfterFailure(SaveChangesStatePreserver state, IReadOnlyList<EntryStateSnapshot> before, Exception original)
    {
        try
        {
            state.Restore(before);
        }
        catch (Exception cleanup)
        {
            original.Data["TrackerRestoreFailure"] = cleanup;
            TransactionFailure = original;
        }
    }
}
