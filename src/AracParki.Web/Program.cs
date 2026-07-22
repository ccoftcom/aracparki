using System.Globalization;
using System.Threading.RateLimiting;
using System.Security.Claims;
using AracParki.Application.Email;
using AracParki.Application;
using AracParki.Application.Authorization;
using AracParki.Application.Catalog.Services;
using AracParki.Application.Listings;
using AracParki.Application.Listings.Services;
using AracParki.Application.Media;
using AracParki.Infrastructure;
using AracParki.Infrastructure.Persistence;
using AracParki.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, _, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Default: only loopback is trusted. Opt-in TrustAllProxies only behind a known edge proxy.
        if (builder.Configuration.GetValue("ForwardedHeaders:TrustAllProxies", false))
        {
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        }
    });

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddRazorPages();
    builder.Services.AddAntiforgery();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
    });
    builder.Services.AddSingleton<SiteUrls>();
    builder.Services.Configure<SeoSettings>(builder.Configuration.GetSection(SeoSettings.SectionName));
    builder.Services.AddMemoryCache();
    builder.Services.Configure<RequestLocalizationOptions>(options =>
    {
        var supported = new[] { "tr-TR", "en-US", "en-GB", "en" };
        options.SetDefaultCulture("tr-TR")
            .AddSupportedCultures(supported)
            .AddSupportedUICultures(supported);
        options.ApplyCurrentCultureToResponseHeaders = true;
        options.RequestCultureProviders =
        [
            new AcceptLanguageHeaderRequestCultureProvider(),
            new CookieRequestCultureProvider()
        ];
    });
    var cookieSecure = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    builder.Services.AddSession(options =>
    {
        options.Cookie.Name = "aracparki.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = cookieSecure;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.IdleTimeout = TimeSpan.FromHours(4);
    });

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
            options.Cookie.SecurePolicy = cookieSecure;
            options.Cookie.SameSite = SameSiteMode.Lax;
            AuthCookie.ConfigureSecurityStampValidation(options);
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthPolicies.ListingModerate, policy =>
            policy.RequireRole(AuthRoles.Admin));
        options.AddPolicy(AuthPolicies.ListingPublish, policy =>
            policy.RequireAuthenticatedUser());
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("phone-reveal", httpContext =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var adNo = httpContext.Request.RouteValues.TryGetValue("adNo", out var raw)
                ? raw?.ToString() ?? ""
                : "";
            return RateLimitPartition.GetFixedWindowLimiter(
                $"{ip}:{adNo}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
        });
        options.AddPolicy("phone-otp-verify", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(15),
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
        options.AddPolicy("listing-publish", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.Identity?.Name
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0
                }));
        options.AddPolicy("phone-otp", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 8,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0
                }));
        options.AddPolicy("listing-wizard-upload", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 40,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0
                }));
        options.AddPolicy("listing-images", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0
                }));
    });

    var pg = builder.Configuration.GetConnectionString("PostgreSQL")
        ?? throw new InvalidOperationException("Connection string 'PostgreSQL' is missing.");
    var redis = builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Connection string 'Redis' is missing.");

    builder.Services.AddHealthChecks()
        .AddNpgSql(pg, name: "postgres")
        .AddRedis(redis, name: "redis");

    var app = builder.Build();
    Lucide.Configure(app.Environment);

    await app.Services.GetRequiredService<DatabaseMigrator>().MigrateAsync();

    app.UseForwardedHeaders();

    var defaultCulture = CultureInfo.GetCultureInfo("tr-TR");
    CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
    CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();
    app.UseSecurityHeaders();
    app.UseResponseCompression();
    app.UseHttpsRedirection();
    var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider
    {
        Mappings =
        {
            [".webmanifest"] = "application/manifest+json"
        }
    };
    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = contentTypes,
        OnPrepareResponse = ctx =>
        {
            // styles.css @import zinciri tarayıcıda agresif cache'lenir; Dev'de anlık CSS/JS için kapat.
            if (app.Environment.IsDevelopment())
            {
                ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                ctx.Context.Response.Headers.Pragma = "no-cache";
                ctx.Context.Response.Headers.Expires = "0";
            }
        }
    });
    app.UseRouting();
    app.UseRequestLocalization();
    app.UseRateLimiter();
    app.UseSession();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapHealthChecks("/health");
    app.MapSitemaps();
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
        .RequireRateLimiting("phone-reveal");

    var locations = app.MapGroup("/api/locations");
    locations.MapGet("/cities", async (CatalogService catalog, CancellationToken ct) =>
        Results.Json(await catalog.GetAllCitiesAsync(ct)));
    locations.MapGet("/cities/{cityId:int}/districts", async (int cityId, CatalogService catalog, CancellationToken ct) =>
        Results.Json(await catalog.GetDistrictsByCityAsync(cityId, ct)));
    locations.MapGet("/districts", async (string? cityIds, CatalogService catalog, CancellationToken ct) =>
    {
        var ids = (cityIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        return Results.Json(await catalog.GetDistrictsByCitiesAsync(ids, ct));
    });
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

    var listingImages = app.MapGroup("/api/listings/{adNo}/images")
        .RequireAuthorization()
        .RequireRateLimiting("listing-images");

    listingImages.MapGet("/", async (
            string adNo,
            IListingImageCommandStore images,
            IOptions<CloudflareMediaSettings> mediaOptions,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (!TryAccountId(user, out var accountId))
            {
                return Results.Unauthorized();
            }

            var items = await images.ListByAdNoForAccountAsync(adNo, accountId, ct);
            if (items.Count == 0)
            {
                // Distinguish empty listing vs not owned: ownership miss also returns empty.
                // Clients treat [] as no images or no access.
            }

            var publicBase = mediaOptions.Value.ResolvedPublicBaseUrl;
            return Results.Json(items.Select(i => new
            {
                id = i.Id,
                url = i.Url,
                imageId = i.ImageId,
                storageKey = i.StorageKey,
                sortOrder = i.SortOrder,
                isCover = i.IsCover,
                width = i.Width,
                height = i.Height,
                mimeType = i.MimeType,
                version = i.Version,
                variants = string.IsNullOrWhiteSpace(i.StorageKey) || string.IsNullOrWhiteSpace(publicBase)
                    ? null
                    : ListingImageVariants.All(publicBase, i.StorageKey)
            }));
        })
        .DisableAntiforgery();

    listingImages.MapGet("/{imageId:long}/variants", async (
            string adNo,
            long imageId,
            IListingImageCommandStore images,
            IOptions<CloudflareMediaSettings> mediaOptions,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (!TryAccountId(user, out var accountId))
            {
                return Results.Unauthorized();
            }

            var item = (await images.ListByAdNoForAccountAsync(adNo, accountId, ct))
                .FirstOrDefault(i => i.Id == imageId);
            if (item is null)
            {
                return Results.NotFound();
            }

            var publicBase = mediaOptions.Value.ResolvedPublicBaseUrl;
            if (string.IsNullOrWhiteSpace(item.StorageKey) || string.IsNullOrWhiteSpace(publicBase))
            {
                return Results.Json(new { url = item.Url, variants = new { card = item.Url } });
            }

            return Results.Json(new
            {
                id = item.Id,
                storageKey = item.StorageKey,
                variants = ListingImageVariants.All(publicBase, item.StorageKey)
            });
        })
        .DisableAntiforgery();

    listingImages.MapPatch("/reorder", async (
            string adNo,
            ReorderImagesRequest body,
            IListingImageCommandStore images,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (!TryAccountId(user, out var accountId))
            {
                return Results.Unauthorized();
            }

            if (body.ImageIds is null || body.ImageIds.Count == 0)
            {
                return Results.BadRequest(new { error = "imageIds required" });
            }

            var ok = await images.ReorderAsync(adNo, accountId, body.ImageIds, ct);
            return ok ? Results.NoContent() : Results.BadRequest(new { error = "reorder_failed" });
        });

    listingImages.MapPatch("/{imageId:long}/cover", async (
            string adNo,
            long imageId,
            IListingImageCommandStore images,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (!TryAccountId(user, out var accountId))
            {
                return Results.Unauthorized();
            }

            var ok = await images.SetCoverAsync(adNo, accountId, imageId, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

    listingImages.MapDelete("/{imageId:long}", async (
            string adNo,
            long imageId,
            IListingImageCommandStore images,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (!TryAccountId(user, out var accountId))
            {
                return Results.Unauthorized();
            }

            var ok = await images.SoftDeleteAsync(adNo, accountId, imageId, TimeSpan.FromDays(7), ct);
            return ok ? Results.NoContent() : Results.BadRequest(new { error = "delete_failed" });
        });

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

static bool TryAccountId(ClaimsPrincipal user, out long accountId)
{
    accountId = 0;
    var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
    return long.TryParse(raw, out accountId);
}

file sealed record ReorderImagesRequest(IReadOnlyList<long>? ImageIds);

public partial class Program;
