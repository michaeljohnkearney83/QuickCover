using Mediator;
using QuickCover.Application.Common;

namespace QuickCover.Application.Quotes;

public sealed record GetQuoteQuery(Guid QuoteId, string CustomerId) : IQuery<QuoteResponse>;

public sealed class GetQuoteHandler(IQuoteRepository quotes) : IQueryHandler<GetQuoteQuery, QuoteResponse>
{
    public async ValueTask<QuoteResponse> Handle(GetQuoteQuery query, CancellationToken cancellationToken)
    {
        var quote = await quotes.GetAsync(query.QuoteId, cancellationToken);

        // Someone else's quote returns 404, not 403, so we don't reveal that it exists.
        if (quote is null || quote.CustomerId != query.CustomerId)
            throw new NotFoundException($"Quote {query.QuoteId} was not found.");

        return QuoteResponse.From(quote);
    }
}
