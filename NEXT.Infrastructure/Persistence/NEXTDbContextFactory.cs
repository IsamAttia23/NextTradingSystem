using NEXT.Application.Abstractions.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using NEXT.Infrastructure.Services;
using NEXT.Infrastructure.Persistence.SaveChanges;

namespace NEXT.Infrastructure.Persistence;

/// <summary>Allows EF Core tooling to create the context for migrations without starting the web host.</summary>
public sealed class NEXTDbContextFactory : IDesignTimeDbContextFactory<NEXTDbContext>
{
    public NEXTDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
     .AddUserSecrets("57f7acf9-66bb-4e86-a018-95f71c8d9629")
     .AddEnvironmentVariables()
     .Build();

        var connectionString =
            configuration.GetConnectionString("NEXTDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'NEXTDatabase' was not found.");
        }
        var options = new DbContextOptionsBuilder<NEXTDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // EF tooling does not execute request-scoped auditing; these no-op services are design-time only.
        var currentUser = new DesignTimeCurrentUserService();
        var requestMetadata = new DesignTimeRequestMetadataService();
        var valueSerializer = new AuditValueSerializer();
        var softDeleteHandler = new SoftDeleteHandler(valueSerializer);
        var auditFieldsHandler = new AuditFieldsHandler();
        var trackedChangesProcessor = new TrackedChangesProcessor(
            currentUser, valueSerializer, softDeleteHandler, auditFieldsHandler);
        var auditEntryFactory = new AuditEntryFactory(
            currentUser, requestMetadata, valueSerializer);

        return new NEXTDbContext(options, trackedChangesProcessor, auditEntryFactory);
    }

    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
    }

    private sealed class DesignTimeRequestMetadataService : IRequestMetadataService
    {
        public string? IpAddress => null;
        public string? UserAgent => null;
    }
}
