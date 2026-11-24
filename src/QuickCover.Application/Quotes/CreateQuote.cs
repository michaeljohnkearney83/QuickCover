using Mediator;
using QuickCover.Application.Common;
using QuickCover.Domain.Quotes;

namespace QuickCover.Application.Quotes;

public sealed record CreateQuoteCommand(string CustomerId, QuoteRequest Request)
    : ICommand<QuoteResponse>, IValidatable
{
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(CustomerId))
            yield return "Customer id is required.";

        foreach (var error in Request.Validate())
            yield return error;
    }
}

public sealed class CreateQuoteHandler(
    IRatingFactorProvider ratingFactors,
    IQuoteRepository quotes,
    TimeProvider time) : ICommandHandler<CreateQuoteCommand, QuoteResponse>
{
    public async ValueTask<QuoteResponse> Handle(CreateQuoteCommand command, CancellationToken cancellationToken)
    {
        var factors = await ratingFactors.GetAsync(cancellationToken);
        var premium = PricingEngine.CalculateAnnualPremium(command.Request, factors);

        var quote = Quote.Create(command.CustomerId, command.Request, premium, time.GetUtcNow().UtcDateTime);
        await quotes.AddAsync(quote, cancellationToken);

        return QuoteResponse.From(quote);
    }
}
