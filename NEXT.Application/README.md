# NEXT.Application

Contains use cases, DTOs, validation, query models, and application-owned ports for persistence, identity, request context and pagination.
References Domain and selected Extensions/Mapster/MediatR packages. EF Core query execution and HTTP handling belong to outer layers.

Each top-level type lives in its own matching file. Web form, table, pager, and display models live in NEXT.web/Models. Application read DTOs remain here.

CQRS contracts:
- GetEntityByIdQuery returns a required DTO or throws NotFoundException. IQueryExecutor executes projected queries in Infrastructure without pagination/counting.
- List queries validate PaginationQuery before using the repository filter and sorting functions. GetPagedList applies pagination separately. FindAsync remains a deferred IQueryable contract.
- UpdateEntityCommand's envelope Id is authoritative. Product, Customer, and Supplier updates require the RowVersion returned by the read DTO; stale tokens produce ConflictException. Web forms transport this token as Base64.
- Create/update results are saved entity snapshots. Read queries provide enriched relationship fields when needed.
- User commands and related queries carry UserId instead of a caller-owned ApplicationUser. GetUsersAsync returns lightweight UserResponseDto values.

Account creation is saved before optional role assignment. A role-assignment failure returns a successful save with RoleAssignmentFailed in Warnings; the user remains saved without a role. The UI marks users without roles until a role is assigned. Failed Identity results roll back only their own operation. Relational nested operations use savepoints and restore the enclosing tracked state when an inner failure is handled. Cancellation propagates through I/O ports; Identity APIs without token support are checked before and after completion.

AppException.Code describes an application error. Web translates it to HTTP; unexpected failures retain their diagnostic cause and return a generic response with traceId. Transaction conflicts are surfaced without automatic retries. InMemory test storage has no rollback guarantee; transaction behavior is tested with PostgreSQL.

Mapster configuration belongs to each service provider. Registration rejects conflicting entity/DTO mappings. Input normalization is explicit and precedes validation. Required identifiers reject Guid.Empty.

RelatedRecordDto carries raw, allowlisted column values; Web owns formatting and localization. Recycle-bin use cases coordinate lifecycle handlers and transactions here; IRecycleBinStore supplies infrastructure reads and purge operations. IUserDeletionPolicy makes the last-administrator decision using IAdministratorLookup facts. Temporary development access deliberately retains the fixed Admin role.
