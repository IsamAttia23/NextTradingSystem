using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Infrastructure;
using System.Net.Http.Headers;

namespace NEXT.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddInfrastructureServices(configuration);

            return services;
        }
    }
}
