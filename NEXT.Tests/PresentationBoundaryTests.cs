using NEXT.web.Controllers;
using Xunit;
namespace NEXT.Tests;
public sealed class PresentationBoundaryTests
{
    [Fact]
    public void ControllersDoNotConsumeDomainPersistenceOrIdentityFrameworkServices()
    {
        var controllers = typeof(HomeController).Assembly.GetTypes().Where(x => x.Namespace == "NEXT.web.Controllers");
        foreach (var controller in controllers)
        foreach (var constructor in controller.GetConstructors())
        foreach (var parameter in constructor.GetParameters())
            Check(parameter.ParameterType);
        static void Check(Type type)
        {
            Assert.False(type.Namespace?.StartsWith("NEXT.Domain") == true || type.Namespace?.StartsWith("NEXT.Infrastructure") == true ||
                type.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true || type.Namespace?.StartsWith("Microsoft.AspNetCore.Identity") == true,
                "Forbidden controller dependency: " + type.FullName);
            foreach (var argument in type.GenericTypeArguments) Check(argument);
        }
    }
    [Fact]
    public void ApplicationDtoContractsContainNoHttpFileTypes()
    {
        foreach (var type in typeof(NEXT.Application.DTOs.Authentication.LoginDto).Assembly.GetTypes().Where(x => x.Namespace?.StartsWith("NEXT.Application.DTOs") == true))
        foreach (var property in type.GetProperties())
            Assert.False(property.PropertyType.Namespace?.StartsWith("Microsoft.AspNetCore") == true);
    }
}
