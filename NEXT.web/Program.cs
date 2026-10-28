using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using NEXT.web.Middleware;
using NEXT.Application;
using NEXT.Infrastructure;
using NEXT.web;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;
// Add services to the container.

services.AddApplicationServices(configuration);
services.AddInfrastructureServices(configuration);
services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
}).AddIdentityCookies();

services.AddPresentation(configuration);
if (builder.Environment.IsDevelopment() && configuration.GetValue<bool>("DevelopmentAccess:EphemeralKeys"))
    services.AddDataProtection().UseEphemeralDataProtectionProvider();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<NEXT.Infrastructure.Identity.TemporaryAdministratorBootstrapper>().InitializeAsync();
app.UseStatusCodePagesWithReExecute("/Status/{0}");
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRequestLocalization();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
