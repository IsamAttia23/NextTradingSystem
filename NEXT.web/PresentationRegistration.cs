using NEXT.web.Models;
using NEXT.Application.DTOs.Products;
using NEXT.Application.DTOs.Customers;
using NEXT.Application.DTOs.CustomerContacts;
using NEXT.Application.DTOs.Tags;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.web;

public static class PresentationRegistration
{
    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options => options.MultipartBodyLengthLimit = 8 * 1024 * 1024);
        services.AddScoped<Filters.OperationNotificationFilter>();
        services.AddScoped<Filters.AccountSecurityResultFilter>();
        services.AddScoped<Services.AccountPresenter>();
        services.AddScoped<Services.StatusPresenter>();
        services.AddScoped<Services.AccountFlowCookies>();
        services.AddScoped(typeof(Services.EntityPresenter<,>));
        services.AddScoped<Services.EntityPresenter<ProductDto, ProductInputDto>, Services.ProductPresenter>();
        services.AddScoped<Services.EntityPresenter<CustomerDto, CustomerInputDto>, Services.CustomerPresenter>();
        services.AddScoped<Services.EntityPresenter<CustomerContactDto, CustomerContactInputDto>, Services.CustomerContactPresenter>();
        services.AddScoped<Services.EntityPresenter<TagDto, TagInputDto>, Services.TagPresenter>();
        services.AddScoped<Services.InventoryPresenter>();
        services.AddScoped<Services.QueryFilterPresenter>();
        services.AddScoped<Services.FormFieldPresenter>();
        services.AddScoped<Services.TablePresenter>();
        services.AddScoped<Services.RecycleBinPresenter>();
        services.AddScoped<Services.UsersPresenter>();
        services.AddScoped<Services.RolesPresenter>();
        services.AddScoped<Services.PermissionsPresenter>();
        services.AddScoped<Services.AssignmentsPresenter>();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddControllersWithViews(options =>
        {
            var scalarBinding = new Services.HtmlScalarModelBinderProvider();
            options.ModelBinderProviders.Insert(0, scalarBinding);
            options.ModelMetadataDetailsProviders.Add(scalarBinding);
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            options.Filters.AddService<Filters.OperationNotificationFilter>();
            options.Filters.AddService<Filters.AccountSecurityResultFilter>();
        })
            .AddDataAnnotationsLocalization(options => options.DataAnnotationLocalizerProvider =
                (_, factory) => factory.Create(typeof(Localization.UiText)));
        services.Configure<RequestLocalizationOptions>(options => options
            .SetDefaultCulture("ar").AddSupportedCultures("ar", "en").AddSupportedUICultures("ar", "en"));
        services.Configure<RequestLocalizationOptions>(options => options.RequestCultureProviders =
        [
            new CookieRequestCultureProvider(),
            new QueryStringRequestCultureProvider(),
            new AcceptLanguageHeaderRequestCultureProvider()
        ]);
        var roles = configuration.GetSection("Security:AdministratorRoles").Get<string[]>() ?? ["Admin"];
        services.AddAuthorizationBuilder().AddPolicy("ManageSecurity", policy =>
            policy.RequireAuthenticatedUser().RequireRole(roles));
        services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            options.Events.OnValidatePrincipal = async context =>
            {
                var access = context.HttpContext.RequestServices.GetRequiredService<IAccountAccess>();
                if (!await access.IsSessionValidAsync(context.Principal))
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
        if (context.Request.Path.StartsWithSegments("/api") ||
            context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            context.Response.StatusCode = status;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }
        return Task.CompletedTask;
    }
}
