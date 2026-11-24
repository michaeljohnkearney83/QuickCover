using QuickCover.DocsWorker;
using QuickCover.Infrastructure;
using QuestPDF.Infrastructure;

// Community licence: free for individuals and businesses under $1M annual revenue. See Directory.Packages.props.
QuestPDF.Settings.License = LicenseType.Community;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PolicyDocumentRenderer>();
builder.Services.AddDocumentStorage(builder.Configuration);

// No SQL here: this service only reads events and writes blobs. Overwriting the same blob name
// makes redelivery harmless, so it doesn't need the SQL inbox/outbox.
builder.Services.AddMessaging(builder.Configuration, x => x.AddConsumer<PolicyIssuedConsumer>(), useSqlOutbox: false);

builder.Build().Run();
