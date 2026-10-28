using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NEXT.Application;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.RecycleBin;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Infrastructure;
using NEXT.Infrastructure.Persistence;
using NEXT.Infrastructure.Persistence.Repositories;
using NEXT.Infrastructure.Persistence.SaveChanges;
using NEXT.web.Middleware;
using Xunit;

namespace NEXT.Tests;

public class ArchitectureAndRegressionTests
{
    [Fact]
    public void AllFivePermissionRequestHandlersResolve()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        Type[] definitions = [
            typeof(NEXT.Application.Features.Common.CQRS.CreateEntityCommand<,,,>),
            typeof(NEXT.Application.Features.Common.CQRS.UpdateEntityCommand<,,,>),
            typeof(NEXT.Application.Features.Common.CQRS.DeleteEntityCommand<,,,>),
            typeof(NEXT.Application.Features.Common.CQRS.GetEntityByIdQuery<,,,>),
            typeof(NEXT.Application.Features.Common.CQRS.GetEntitiesQuery<,,,>)];
        foreach (var definition in definitions)
        {
            var request = definition.MakeGenericType(typeof(Guid), typeof(PermissionDto),
                typeof(PermissionCreationDto), typeof(PermissionUpdateDto));
            var responseContract = request.GetInterfaces().SingleOrDefault(type =>
                type.IsGenericType && type.GetGenericTypeDefinition() == typeof(MediatR.IRequest<>));
            var handler = responseContract is null
                ? typeof(MediatR.IRequestHandler<>).MakeGenericType(request)
                : typeof(MediatR.IRequestHandler<,>).MakeGenericType(request, responseContract.GenericTypeArguments[0]);
            Assert.NotNull(scope.ServiceProvider.GetRequiredService(handler));
        }
    }

    [Fact]
    public async Task MediatorResolvesPermissionCreateAndRunsValidation()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        var request = new NEXT.Application.Features.Common.CQRS.CreateEntityCommand<Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto>(new());
        await Assert.ThrowsAsync<AppValidationException>(() => mediator.Send(request));
    }

    private sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "NEXT.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:NEXTDatabase"] = "Host=localhost;Database=next_tests;Username=test;Password=test" }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new TestHostEnvironment());
        services.AddAuthentication(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddApplicationServices(configuration);
        services.AddInfrastructureServices(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void ApplicationDoesNotReferenceEfOrInfrastructureOrHttp()
    {
        var dependencies = typeof(EntityServiceRegistrationExtensions).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(dependencies, x => x.Name!.StartsWith("Microsoft.EntityFrameworkCore") ||
            x.Name.StartsWith("NEXT.Infrastructure") || x.Name.StartsWith("Microsoft.AspNetCore"));
    }

    [Fact]
    public void AllDedicatedRepositoriesAndRequestContextResolve()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var contracts = typeof(IPermissionRepository).Assembly.GetTypes().Where(x => x.IsInterface &&
            x.Namespace == "NEXT.Application.Abstractions.Persistence.Repositories");
        foreach (var contract in contracts)
            Assert.NotNull(scope.ServiceProvider.GetRequiredService(contract));
        Assert.Null(scope.ServiceProvider.GetRequiredService<ICurrentUserService>().UserId);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRecycleBinService<Tag, Guid>>());
    }

    [Fact]
    public async Task RepositoryDeletionUsesSoftDeletePipelineAndKeepsSnapshot()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Wholesale", Color = "blue" };
        db.Attach(tag);
        await new KeyRepository<Tag, Guid>(db).DeleteAsync(tag);
        Assert.Equal(EntityState.Deleted, db.Entry(tag).State);
        var now = DateTimeOffset.UtcNow;
        var user = Guid.NewGuid();
        Assert.True(scope.ServiceProvider.GetRequiredService<SoftDeleteHandler>().TryHandle(db, db.Entry(tag), now, user));
        Assert.Equal(EntityState.Modified, db.Entry(tag).State);
        Assert.Equal(now, tag.DeletedAt);
        Assert.Equal(user, tag.DeletedBy);
        var snapshot = Assert.Single(db.ChangeTracker.Entries<RecycleBinEntry>());
        Assert.Equal(tag.Id, snapshot.Entity.EntityId);
        Assert.Contains("Wholesale", snapshot.Entity.EntityDataJson);
    }

    [Fact]
    public async Task NonSoftDeletableEntityIsActuallyMarkedForDeletion()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var permission = new Permission { Id = Guid.NewGuid(), Code = "READ", Module = "Tests" };
        db.Attach(permission);
        await new KeyRepository<Permission, Guid>(db).DeleteAsync(permission);
        Assert.Equal(EntityState.Deleted, db.Entry(permission).State);
    }

    [Fact]
    public async Task PermissionServiceValidatesBeforeCallingDatabase()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.IServices.IEntityServices<Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto>>();
        await Assert.ThrowsAsync<AppValidationException>(() => service.Add(new PermissionCreationDto { Code = null! }));
    }

    [Fact]
    public async Task UnexpectedErrorsDoNotExposeInternalDetails()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new InvalidOperationException("database-password"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(500, context.Response.StatusCode);
        Assert.DoesNotContain("database-password", body);
        Assert.Contains("traceId", body);
    }

    [Fact]
    public async Task ValidationErrorsAreReturnedWithFields()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new AppValidationException(
            new Dictionary<string, string[]> { ["Code"] = ["Required"] }), NullLogger<ExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal("Required", json.RootElement.GetProperty("errors").GetProperty("Code")[0].GetString());
    }
}
