using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AracParki.Infrastructure.Caching;

public static class RedisServiceCollectionExtensions
{
    public const string ConnectionStringName = "Redis";

    /// <summary>Key prefix for <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> entries.</summary>
    public const string InstanceName = "aracparki:";

    /// <summary>
    /// Shared <see cref="IConnectionMultiplexer"/> (singleton — StackExchange.Redis best practice)
    /// plus <c>IDistributedCache</c> backed by the same multiplexer (ASP.NET Core Redis cache docs).
    /// </summary>
    public static IServiceCollection AddAracParkiRedis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is missing.");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(connectionString);
            // Allow app boot while docker-compose Redis is still starting.
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 3;
            options.ConnectTimeout = 5_000;
            options.SyncTimeout = 5_000;
            options.AsyncTimeout = 5_000;
            options.KeepAlive = 60;
            options.ClientName = "aracparki-web";
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddStackExchangeRedisCache(_ => { });
        services.AddOptions<RedisCacheOptions>()
            .Configure<IConnectionMultiplexer>((options, multiplexer) =>
            {
                options.InstanceName = InstanceName;
                options.ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer);
            });

        return services;
    }
}
