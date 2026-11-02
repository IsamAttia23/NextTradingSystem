# NEXT.Infrastructure

.NET 10 / EF Core 10 / PostgreSQL adapters for application ports.

Call `AddInfrastructureServices(configuration)` from the Web composition root. Configure `ConnectionStrings:NEXTDatabase` using environment variables or user secrets. Repositories share a scoped NEXTDbContext. Inject the required repository directly; IUnitOfWork coordinates SaveChanges and transactions.

Entities use Guid keys. Composite keys use IRepository.GetByKeyAsync with all key values. Dedicated repository contracts are registered from their implementations. Pagination materialization runs here rather than in Application.

SaveChanges applies audit fields, converts soft deletions, writes recycle snapshots and audit logs, and generates application-managed byte[] concurrency tokens. The recycle-bin service uses IgnoreQueryFilters for deleted entities and a separate explicit purge path. Authorization must be enforced by the calling use case or endpoint.

Both save stages share one owned transaction or one savepoint inside an existing transaction. Failed writes restore scalar tracked state (including generated/temporary keys) and remove newly created audit rows from the tracker. SaveChanges(false) retains pending state without writing the entity twice during the call. A rollback failure poisons the context so the enclosing unit of work cannot continue committing. Cleanup uses an independent bounded cancellation token and preserves the original exception even if logging fails.

StorageErrors translates known PostgreSQL/concurrency failures at execution boundaries. Deferred IQueryable queries must be materialized through the application execution/pagination ports to use that boundary. UnitOfWork coordinates application transactions; it does not retry writes automatically. InMemory has no database rollback guarantee.

NormalizedEmail is unique across all rows, including soft-deleted users. Physical row deletion releases the address. Uniqueness checks use EXISTS and include soft-deleted accounts. The database constraint is installed by 20261102105206_ReconcileInfrastructureSchema; the migration refuses existing duplicate normalized emails without modifying them. Review and resolve any duplicates explicitly before applying it.

The same migration reconciles partner image columns and inventory/warehouse indexes previously supplied in docs/sql. It supports both fresh migration-based databases and databases with those additive SQL upgrades already applied. Do not edit historical migrations. The generated upgrade SQL requires the database to have the preceding migrations recorded.

RelatedRecordsReader returns only raw allowlisted columns. PostgreSQL self-relations use a parameterized recursive CTE with duplicate elimination and existing DeletedAt visibility. All descendant identifiers are still collected to preserve complete counts; no arbitrary hierarchy depth limit is imposed. Formatting is in Web. Profile images use a single resource-limited decoder, asynchronous cancellable admission for request handlers, and a synchronous compatibility entry point. Native progress cancellation is cooperative, not a hard execution deadline.

Infrastructure implements IRecycleBinStore; Application owns lifecycle orchestration and encloses handlers in the same transaction. The unused OnSoftDeleteAsync hook was removed from IRecycleBinHandler; SaveChanges keeps the existing soft-delete pipeline. User-deletion business policy lives in Application, while AdministratorLookup queries normalized configured role names. The temporary development administrator remains Admin by explicit policy.

See [the review](../docs/ARCHITECTURE-REVIEW.ar.md) for migration and compatibility details, verification results, and remaining work.
