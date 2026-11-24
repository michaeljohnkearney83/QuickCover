using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using QuickCover.Application.Common;
using QuickCover.Domain.Quotes;

namespace QuickCover.Infrastructure.Mongo;

/// <summary>
/// Persistence model for a quote. Kept separate from the domain Quote so storage concerns
/// (ids as strings, Decimal128, enum names) never leak into the domain.
/// </summary>
internal sealed class QuoteDocument
{
    [BsonId] public string Id { get; set; } = default!;
    public string CustomerId { get; set; } = default!;
    public int DriverAge { get; set; }
    public int YearsLicensed { get; set; }
    public int ClaimsLast5Years { get; set; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal VehicleValue { get; set; }
    public string Use { get; set; } = default!;
    public string PostcodeArea { get; set; } = default!;
    [BsonRepresentation(BsonType.Decimal128)] public decimal AnnualPremium { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? PurchasedUtc { get; set; }

    public static QuoteDocument From(Quote q) => new()
    {
        Id = q.Id.ToString(),
        CustomerId = q.CustomerId,
        DriverAge = q.Request.DriverAge,
        YearsLicensed = q.Request.YearsLicensed,
        ClaimsLast5Years = q.Request.ClaimsLast5Years,
        VehicleValue = q.Request.VehicleValue,
        Use = q.Request.Use.ToString(),
        PostcodeArea = q.Request.PostcodeArea,
        AnnualPremium = q.AnnualPremium,
        Status = q.Status.ToString(),
        CreatedUtc = q.CreatedUtc,
        ExpiresUtc = q.ExpiresUtc,
        PurchasedUtc = q.PurchasedUtc
    };

    public Quote ToDomain() => Quote.Rehydrate(
        Guid.Parse(Id),
        CustomerId,
        new QuoteRequest(DriverAge, YearsLicensed, ClaimsLast5Years, VehicleValue, Enum.Parse<VehicleUse>(Use), PostcodeArea),
        AnnualPremium,
        Enum.Parse<QuoteStatus>(Status),
        DateTime.SpecifyKind(CreatedUtc, DateTimeKind.Utc),
        DateTime.SpecifyKind(ExpiresUtc, DateTimeKind.Utc),
        PurchasedUtc is null ? null : DateTime.SpecifyKind(PurchasedUtc.Value, DateTimeKind.Utc));
}

/// <summary>MongoDB locally; Azure DocumentDB (MongoDB compatible) or Atlas in the cloud. Same driver either way.</summary>
internal sealed class MongoQuoteRepository(IMongoDatabase database) : IQuoteRepository
{
    private readonly IMongoCollection<QuoteDocument> _quotes = database.GetCollection<QuoteDocument>("quotes");

    public Task AddAsync(Quote quote, CancellationToken ct) =>
        _quotes.InsertOneAsync(QuoteDocument.From(quote), cancellationToken: ct);

    public async Task<Quote?> GetAsync(Guid id, CancellationToken ct)
    {
        var key = id.ToString();
        var doc = await _quotes.Find(d => d.Id == key).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public Task UpdateAsync(Quote quote, CancellationToken ct)
    {
        var key = quote.Id.ToString();
        return _quotes.ReplaceOneAsync(d => d.Id == key, QuoteDocument.From(quote), cancellationToken: ct);
    }
}
