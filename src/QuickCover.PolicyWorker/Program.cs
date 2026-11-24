using QuickCover.Infrastructure;
using QuickCover.PolicyWorker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSqlPersistence(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration, x => x.AddConsumer<QuotePurchasedConsumer>());

var host = builder.Build();

// The API and worker may start together; both are allowed to create the dev database.
await host.Services.EnsureDatabaseCreatedAsync();

host.Run();
