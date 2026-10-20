using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
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

        builder.Entity<UserRole>()
            .ToTable("UserRoles", "security");

        builder.Entity<IdentityUserLogin<Guid>>()
            .ToTable("UserLogins", "security");

        builder.Entity<IdentityUserToken<Guid>>()
            .ToTable("UserTokens", "security");

        builder.Entity<Role>()
            .ToTable("Roles", "security");

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
                continue;

            var parameter = Expression.Parameter(entityClrType, "entity");
            var deletedAt = Expression.Property(parameter, nameof(IsDeletableEntity<Guid>.DeletedAt));
            var isNotDeleted = Expression.Equal(
                deletedAt,
                Expression.Constant(null, typeof(DateTimeOffset?)));
            var filter = Expression.Lambda(isNotDeleted, parameter);

            builder.Entity(entityClrType).HasQueryFilter(filter);
        }
    }

    public override int SaveChanges() => SaveChanges(acceptAllChangesOnSuccess: true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        IDbContextTransaction? transaction = null;
        try
        {
            if (Database.IsRelational() && Database.CurrentTransaction is null)
                transaction = Database.BeginTransaction();

            var pendingAuditEntries = trackedChangesProcessor.Prepare(this);
            var statePreserver = new SaveChangesStatePreserver(this);
            var stateSnapshots = acceptAllChangesOnSuccess ? null : statePreserver.Capture();
            var result = base.SaveChanges(acceptAllChangesOnSuccess);

            if (pendingAuditEntries.Count == 0)
            {
                transaction?.Commit();
                return result;
            }

            var auditRowsCount = auditEntryFactory.AddAuditRows(this, pendingAuditEntries);
            if (auditRowsCount > 0)
            {
                if (stateSnapshots is not null)
                    statePreserver.MarkUnchanged(stateSnapshots);

                try
                {
                    result += base.SaveChanges(acceptAllChangesOnSuccess);
                }
                finally
                {
                    if (stateSnapshots is not null)
                        statePreserver.Restore(stateSnapshots);
                }
            }

            transaction?.Commit();
            return result;
        }
        catch
        {
            transaction?.Rollback();
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? transaction = null;
        try
        {
            if (Database.IsRelational() && Database.CurrentTransaction is null)
                transaction = await Database.BeginTransactionAsync(cancellationToken);

            var pendingAuditEntries = trackedChangesProcessor.Prepare(this);
            var statePreserver = new SaveChangesStatePreserver(this);
            var stateSnapshots = acceptAllChangesOnSuccess ? null : statePreserver.Capture();
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            if (pendingAuditEntries.Count == 0)
            {
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
                return result;
            }

            var auditRowsCount = auditEntryFactory.AddAuditRows(this, pendingAuditEntries);
            if (auditRowsCount > 0)
            {
                if (stateSnapshots is not null)
                    statePreserver.MarkUnchanged(stateSnapshots);

                try
                {
                    result += await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
                }
                finally
                {
                    if (stateSnapshots is not null)
                        statePreserver.Restore(stateSnapshots);
                }
            }

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }
}
