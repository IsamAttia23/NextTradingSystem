using Microsoft.AspNetCore.Authorization;
using NEXT.Application.Abstractions.Context;

namespace NEXT.web.Services;

public sealed class RootStoreRequirement : IAuthorizationRequirement { }

public sealed class RootStoreAuthorization(ICurrentStore store) : AuthorizationHandler<RootStoreRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RootStoreRequirement requirement)
    {
        if (store.IsDefault) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
