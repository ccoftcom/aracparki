using AracParki.Application.Abstractions;
using AracParki.Application.Accounts;
using AracParki.Application.Catalog;
using AracParki.Application.Email;
using AracParki.Application.Listings;
using AracParki.Application.Media;
using AracParki.Application.Messaging;
using AracParki.Application.Corporate;
using AracParki.Infrastructure.Accounts;
using AracParki.Infrastructure.Caching;
using AracParki.Infrastructure.Catalog;
using AracParki.Infrastructure.Corporate;
using AracParki.Infrastructure.Email;
using AracParki.Infrastructure.Listings;
using AracParki.Infrastructure.Messaging;
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
        services.Configure<WhatsAppSettings>(configuration.GetSection(WhatsAppSettings.SectionName));
        services.Configure<CloudflareMediaSettings>(configuration.GetSection(CloudflareMediaSettings.SectionName));

        services.AddAracParkiRedis(configuration);
        services.AddHttpClient(WhatsAppOtpSender.HttpClientName);

        var media = configuration.GetSection(CloudflareMediaSettings.SectionName).Get<CloudflareMediaSettings>()
                    ?? new CloudflareMediaSettings();

        services.AddHttpClient(CloudflareListingImageStorage.HttpClientName, client =>
        {
            if (!string.IsNullOrWhiteSpace(media.WorkerBaseUrl))
            {
                client.BaseAddress = new Uri(media.WorkerBaseUrl.TrimEnd('/') + "/");
            }

            client.Timeout = TimeSpan.FromMinutes(2);
        });

        services.AddSingleton<ISqlQueryLoader, SqlQueryLoader>();
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<DatabaseMigrator>();
        services.AddScoped<IListingQuery, ListingRepository>();
        services.AddScoped<IListingStore, ListingWriteRepository>();
        services.AddScoped<IWizardDraftStore, WizardDraftRepository>();
        services.AddScoped<IListingImageCommandStore, ListingImageCommandStore>();

        if (media.IsConfigured)
        {
            services.AddScoped<IListingImageStorage, CloudflareListingImageStorage>();
        }
        else
        {
            services.AddScoped<IListingImageStorage, LocalListingImageStorage>();
        }

        services.AddScoped<ICorporateAccountStore, CorporateAccountRepository>();
        services.AddScoped<ICorporateDocumentStorage, LocalCorporateDocumentStorage>();

        services.AddScoped<ICatalogQuery, CatalogRepository>();
        services.AddScoped<IAccountStore, AccountRepository>();
        services.AddScoped<IPhoneOtpStore, PhoneOtpRepository>();
        services.AddScoped<IPhoneOtpService, PhoneOtpService>();
        services.AddScoped<IWhatsAppOtpSender, WhatsAppOtpSender>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        return services;
    }
}
