using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace NEXT.web;

public static class PresentationRegistration
{
    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options => options.MultipartBodyLengthLimit = 8 * 1024 * 1024);
        services.AddScoped<Filters.OperationNotificationFilter>();
        services.AddScoped<Services.UserFormBuilder>();
        services.AddScoped<Services.PermissionOptionsProvider>();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddControllersWithViews(options => { options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()); options.Filters.AddService<Filters.OperationNotificationFilter>(); })
            .AddDataAnnotationsLocalization(options => options.DataAnnotationLocalizerProvider =
                (_, factory) => factory.Create(typeof(Localization.UiText)));
        services.Configure<RequestLocalizationOptions>(options => options
            .SetDefaultCulture("ar").AddSupportedCultures("ar", "en").AddSupportedUICultures("ar", "en"));
        var roles = configuration.GetSection("Security:AdministratorRoles").Get<string[]>() ?? ["Admin"];
        services.AddAuthorizationBuilder().AddPolicy("ManageSecurity", policy =>
            policy.RequireAuthenticatedUser().RequireRole(roles));
        services.AddPresentationCookies();
        return services;
    }
}
