using QuickCover.Domain.Quotes;

namespace QuickCover.Application.Quotes;

public sealed record QuoteResponse(
    Guid QuoteId,
    decimal AnnualPremium,
    string Status,
    DateTime CreatedUtc,
    DateTime ExpiresUtc)
{
    public static QuoteResponse From(Quote quote) => new(
        quote.Id,
        quote.AnnualPremium,
        quote.Status.ToString(),
        quote.CreatedUtc,
        quote.ExpiresUtc);
}
