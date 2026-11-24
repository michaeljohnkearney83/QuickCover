using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using QuickCover.Application.Common;
using QuickCover.Infrastructure.Caching;
using QuickCover.Infrastructure.Messaging;
using QuickCover.Infrastructure.Mongo;
using QuickCover.Infrastructure.Sql;

namespace QuickCover.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Everything the Quote API needs: SQL, Mongo, Redis.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSqlPersistence(config);

        services.AddSingleton<IMongoClient>(_ => new MongoClient(Required(config, "ConnectionStrings:Mongo")));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(config["Mongo:Database"] ?? "quickcover"));
        services.AddScoped<IQuoteRepository, MongoQuoteRepository>();

        services.AddStackExchangeRedisCache(o =>
        {
            o.Configuration = Required(config, "ConnectionStrings:Redis");
            o.InstanceName = "quickcover:";
        });
        services.AddSingleton<IRatingTableSource, StaticRatingTableSource>();
        services.AddScoped<IRatingFactorProvider, CachedRatingFactorProvider>();

        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
        return services;
    }

    /// <summary>SQL only: shared by the API and the policy worker.</summary>
    public static IServiceCollection AddSqlPersistence(this IServiceCollection services, IConfiguration config)
    {
        services.TryAddSingletonTimeProvider();
        services.AddDbContext<QuickCoverDbContext>(o =>
            o.UseSqlServer(Required(config, "ConnectionStrings:Sql"), sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IPurchaseRepository, EfPurchaseRepository>();
        services.AddScoped<IPolicyReadRepository, EfPolicyReadRepository>();
        return services;
    }

    /// <summary>
    /// MassTransit with the EF Core transactional outbox. The transport is chosen by config:
    /// Messaging:Transport = RabbitMq (default, local + AKS) or AzureServiceBus.
    /// Handlers and consumers don't change at all when you switch.
    /// </summary>
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration config,
        Action<IBusRegistrationConfigurator>? addConsumers = null)
    {
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddEntityFrameworkOutbox<QuickCoverDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();                       // publishes from the API go via the outbox table
                o.QueryDelay = TimeSpan.FromSeconds(1); // how often the delivery service polls
            });

            // Every consumer endpoint: retry transient failures, then use the inbox/outbox so a
            // redelivered message is processed once and any events it publishes are sent exactly once.
            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageRetry(r => r.Intervals(100, 500, 1_000, 5_000));
                endpoint.UseEntityFrameworkOutbox<QuickCoverDbContext>(context);
            });

            addConsumers?.Invoke(x);

            var transport = config["Messaging:Transport"] ?? "RabbitMq";
            if (transport.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase))
            {
                x.UsingAzureServiceBus((context, cfg) =>
                {
                    cfg.Host(Required(config, "ConnectionStrings:ServiceBus"));
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(config["Messaging:RabbitMq:Host"] ?? "localhost", "/", h =>
                    {
                        h.Username(config["Messaging:RabbitMq:Username"] ?? "guest");
                        h.Password(config["Messaging:RabbitMq:Password"] ?? "guest");
                    });
                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        return services;
    }

    /// <summary>
    /// Dev convenience: create the database and tables if missing, retrying while SQL Server starts.
    /// Replace with EF Core migrations before going anywhere near production (see README).
    /// </summary>
    public static async Task EnsureDatabaseCreatedAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSetup");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<QuickCoverDbContext>();
                await db.Database.EnsureCreatedAsync(ct);
                logger.LogInformation("Database ready");
                return;
            }
            catch (Exception ex) when (attempt < 10 && ex is not OperationCanceledException)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}/10): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
            services.AddSingleton(TimeProvider.System);
    }

    private static string Required(IConfiguration config, string key) =>
        config[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing configuration value '{key}'.");
}
