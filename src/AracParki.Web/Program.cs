using System.Threading.RateLimiting;
using AracParki.Application;
using AracParki.Application.Catalog.Services;
using AracParki.Application.Listings.Services;
using AracParki.Infrastructure;
using AracParki.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddRazorPages();
    builder.Services.AddAntiforgery();

    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/giris";
            options.LogoutPath = "/cikis";
            options.AccessDeniedPath = "/giris";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.Cookie.Name = "aracparki.auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
            AuthCookie.ConfigureSecurityStampValidation(options);
        });

    builder.Services.AddAuthorization();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("phone-reveal", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        options.AddPolicy("auth-sensitive", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0
                }));
    });

    var pg = builder.Configuration.GetConnectionString("PostgreSQL")
        ?? throw new InvalidOperationException("Connection string 'PostgreSQL' is missing.");

    builder.Services.AddHealthChecks()
        .AddNpgSql(pg, name: "postgres");

    var app = builder.Build();
    Lucide.Configure(app.Environment);

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();
    app.UseSecurityHeaders();
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapHealthChecks("/health");
    app.MapRazorPages();

    app.MapPost("/ilan/{adNo}/telefon", async (
            string adNo,
            ListingService listings,
            CancellationToken cancellationToken) =>
        {
            var phone = await listings.GetPhoneByAdNoAsync(adNo, cancellationToken);
            if (phone is null)
            {
                return Results.NotFound();
            }

            return Results.Json(new { phone });
        })
        .RequireRateLimiting("phone-reveal")
        .DisableAntiforgery();

    var locations = app.MapGroup("/api/locations");
    locations.MapGet("/cities", async (CatalogService catalog, CancellationToken ct) =>
        Results.Json(await catalog.GetAllCitiesAsync(ct)));
    locations.MapGet("/cities/{cityId:int}/districts", async (int cityId, CatalogService catalog, CancellationToken ct) =>
        Results.Json(await catalog.GetDistrictsByCityAsync(cityId, ct)));
    locations.MapGet("/districts/{districtId:int}/neighborhoods", async (int districtId, CatalogService catalog, CancellationToken ct) =>
        Results.Json(await catalog.GetNeighborhoodsByDistrictAsync(districtId, ct)));
    locations.MapGet("/neighborhoods/{neighborhoodId:int}/streets", async (
            int neighborhoodId,
            string? q,
            CatalogService catalog,
            CancellationToken ct) =>
        Results.Json(await catalog.GetStreetsByNeighborhoodAsync(neighborhoodId, q, ct)));

    var catalogApi = app.MapGroup("/api/catalog");
    catalogApi.MapGet("/categories", async (CatalogService catalog, CancellationToken ct) =>
        Results.Json(await catalog.GetAllCategoriesAsync(ct)));
    catalogApi.MapGet("/category-groups", async (CatalogService catalog, CancellationToken ct) =>
        Results.Json(await catalog.GetCategoryGroupsAsync(ct)));
    catalogApi.MapGet("/brands", async (CatalogService catalog, CancellationToken ct) =>
        Results.Json(await catalog.GetAllBrandsAsync(ct)));
    catalogApi.MapGet("/categories/{categoryId:int}/brands", async (int categoryId, CatalogService catalog, CancellationToken ct) =>
        Results.Json(await catalog.GetBrandsByCategoryAsync(categoryId, ct)));
    catalogApi.MapGet("/categories/{categoryId:int}/brands/{brandId:int}/models", async (
            int categoryId, int brandId, CatalogService catalog, CancellationToken ct) =>
        Results.Json(await catalog.GetModelsByBrandCategoryAsync(brandId, categoryId, ct)));
    catalogApi.MapGet("/categories/{categoryId:int}/attributes", async (int categoryId, CatalogService catalog, CancellationToken ct) =>
        Results.Json(await catalog.GetCategoryAttributesAsync(categoryId, ct)));
    catalogApi.MapGet("/attachments", async (CatalogService catalog, CancellationToken ct) =>
        Results.Json(await catalog.GetAttachmentsAsync(ct)));
    catalogApi.MapGet("/facets/brands", async (int? categoryId, CatalogService catalog, CancellationToken ct) =>
        Results.Json(await catalog.GetBrandFacetsAsync(categoryId, ct)));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
