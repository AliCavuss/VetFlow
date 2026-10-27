using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using VetFlow.Application;
using VetFlow.Domain.Common;
using VetFlow.Infrastructure;
using VetFlow.Infrastructure.Data;
using VetFlow.Infrastructure.Identity;
using VetFlow.Web.Filters;
using VetFlow.Web.Helpers;

// Form values (prices, weights) are posted with '.' as decimal separator; amounts are displayed in Turkish format by Money.Format.
var culture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(10);
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Clinical, p => p.RequireRole(Roles.Admin, Roles.Veterinarian))
    .AddPolicy(Policies.Billing, p => p.RequireRole(Roles.Admin, Roles.Receptionist))
    .AddPolicy(Policies.ManageInventory, p => p.RequireRole(Roles.Admin));

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
    options.Filters.Add<DomainExceptionFilter>();
});

builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");

var app = builder.Build();

await app.Services.InitialiseDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization("en-US");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
