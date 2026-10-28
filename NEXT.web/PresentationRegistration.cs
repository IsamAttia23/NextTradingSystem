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
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddControllersWithViews(options => { options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()); options.Filters.AddService<Filters.OperationNotificationFilter>(); })
            .AddDataAnnotationsLocalization(options => options.DataAnnotationLocalizerProvider =
                (_, factory) => factory.Create(typeof(Localization.UiText)));
        services.Configure<RequestLocalizationOptions>(options => options
            .SetDefaultCulture("ar").AddSupportedCultures("ar", "en").AddSupportedUICultures("ar", "en"));
        var roles = configuration.GetSection("Security:AdministratorRoles").Get<string[]>() ?? ["Admin"];
        services.AddAuthorizationBuilder().AddPolicy("ManageSecurity", policy =>
            policy.RequireAuthenticatedUser().RequireRole(roles));
        services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            options.Events.OnValidatePrincipal = async context =>
            {
                await SecurityStampValidator.ValidatePrincipalAsync(context);
                var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(id, out var userId) || !await context.HttpContext.RequestServices.GetRequiredService<NEXT.Application.Abstractions.Identity.ILoginAccessPolicy>().CanSignInAsync(userId))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/Denied";
            options.Events.OnRedirectToLogin = context => RedirectOrStatus(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => RedirectOrStatus(context, StatusCodes.Status403Forbidden);
        });
        return services;
    }
    private static Task RedirectOrStatus(RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest") context.Response.StatusCode = status;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}
