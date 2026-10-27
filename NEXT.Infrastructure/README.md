# NEXT.Infrastructure

.NET 10 / EF Core 10 / PostgreSQL adapters for application ports.

Call `AddInfrastructureServices(configuration)` from the Web composition root. Configure `ConnectionStrings:NEXTDatabase` using environment variables or user secrets. Repositories share a scoped NEXTDbContext. Inject the required repository directly; IUnitOfWork coordinates SaveChanges and transactions.

Entities use Guid keys. Composite keys use IRepository.GetByKeyAsync with all key values. Dedicated repository contracts are registered from their implementations. Pagination materialization runs here rather than in Application.

SaveChanges applies audit fields, converts soft deletions, writes recycle snapshots and audit logs, and generates application-managed byte[] concurrency tokens. The recycle-bin service uses IgnoreQueryFilters for deleted entities and a separate explicit purge path. Authorization must be enforced by the calling use case or endpoint.

See [the review](../docs/ARCHITECTURE-REVIEW.ar.md) for migration and compatibility details, verification results, and remaining work.
