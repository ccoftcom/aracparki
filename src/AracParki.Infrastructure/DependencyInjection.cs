using AracParki.Application.Abstractions;
using AracParki.Application.Accounts;
using AracParki.Application.Catalog;
using AracParki.Application.Email;
using AracParki.Application.Listings;
using AracParki.Infrastructure.Accounts;
using AracParki.Infrastructure.Catalog;
using AracParki.Infrastructure.Email;
using AracParki.Infrastructure.Listings;
using AracParki.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AracParki.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.Configure<AppSettings>(configuration.GetSection(AppSettings.SectionName));

        services.AddSingleton<ISqlQueryLoader, SqlQueryLoader>();
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<IListingQuery, ListingRepository>();
        services.AddScoped<ICatalogQuery, CatalogRepository>();
        services.AddScoped<IAccountStore, AccountRepository>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        return services;
    }
}
