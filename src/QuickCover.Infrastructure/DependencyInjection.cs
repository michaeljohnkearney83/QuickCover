using Azure.Storage.Blobs;
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
using QuickCover.Infrastructure.Storage;

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
    /// <para>
    /// <paramref name="useSqlOutbox"/> turns on the EF Core outbox/inbox. Services that write to SQL need it.
    /// A service with no SQL (the docs worker) turns it off and relies on its own idempotency instead.
    /// </para>
    /// </summary>
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration config,
        Action<IBusRegistrationConfigurator>? addConsumers = null,
        bool useSqlOutbox = true)
    {
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            if (useSqlOutbox)
            {
                x.AddEntityFrameworkOutbox<QuickCoverDbContext>(o =>
                {
                    o.UseSqlServer();
                    o.UseBusOutbox();                       // publishes from the API go via the outbox table
                    o.QueryDelay = TimeSpan.FromSeconds(1); // how often the delivery service polls
                });
            }

            // Every consumer endpoint: retry transient failures. With the outbox on, the inbox also makes a
            // redelivered message process once and any events it publishes go out exactly once.
            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageRetry(r => r.Intervals(100, 500, 1_000, 5_000));
                if (useSqlOutbox)
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
                    cfg.Host(
                        config["Messaging:RabbitMq:Host"] ?? "localhost",
                        ushort.Parse(config["Messaging:RabbitMq:Port"] ?? "5672"),
                        "/",
                        h =>
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

    /// <summary>Blob storage for policy documents. Used by the API (read) and the docs worker (write).</summary>
    public static IServiceCollection AddDocumentStorage(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(_ => new BlobServiceClient(Required(config, "ConnectionStrings:Blob")));
        services.AddSingleton<IPolicyDocumentStore>(sp => new BlobPolicyDocumentStore(
            sp.GetRequiredService<BlobServiceClient>(),
            config["Storage:PolicyDocumentsContainer"] ?? "policy-documents"));
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
