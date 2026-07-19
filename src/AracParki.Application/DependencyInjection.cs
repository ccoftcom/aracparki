using AracParki.Application.Accounts.Services;
using AracParki.Application.Catalog.Services;
using AracParki.Application.Listings.Services;
using AracParki.Application.Listings.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AracParki.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<ListingSearchQueryValidator>();
        services.AddScoped<ListingService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<AuthEmailService>();
        services.AddScoped<AccountService>();
        return services;
    }
}
