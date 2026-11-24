using Mediator;
using QuickCover.Application.Common;
using QuickCover.Contracts;
using QuickCover.Domain.Policies;

namespace QuickCover.Application.Quotes;

public sealed record PurchaseQuoteCommand(Guid QuoteId, string CustomerId, DateOnly StartDate)
    : ICommand<PurchaseResponse>;

public sealed record PurchaseResponse(Guid PurchaseId, Guid QuoteId, string PaymentReference, string PolicyStatus);

public sealed class PurchaseQuoteHandler(
    IQuoteRepository quotes,
    IPurchaseRepository purchases,
    IEventPublisher events,
    IUnitOfWork unitOfWork,
    TimeProvider time) : ICommandHandler<PurchaseQuoteCommand, PurchaseResponse>
{
    private const int MaxDaysAhead = 30;

    public async ValueTask<PurchaseResponse> Handle(PurchaseQuoteCommand command, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);

        if (command.StartDate < today || command.StartDate > today.AddDays(MaxDaysAhead))
            throw new ValidationException([$"Start date must be between today and {MaxDaysAhead} days from now."]);

        var quote = await quotes.GetAsync(command.QuoteId, cancellationToken);
        if (quote is null || quote.CustomerId != command.CustomerId)
            throw new NotFoundException($"Quote {command.QuoteId} was not found.");

        quote.EnsureCanPurchase(now);

        // Friendly early check. The unique index on Purchases.QuoteId is the real guarantee
        // when two requests race (see EfUnitOfWork).
        if (await purchases.ExistsForQuoteAsync(quote.Id, cancellationToken))
            throw new ConflictException("This quote has already been purchased.");

        var purchase = Purchase.Create(quote.Id, quote.CustomerId, quote.AnnualPremium, command.StartDate, now);
        purchases.Add(purchase);

        await events.PublishAsync(new QuotePurchased(
            purchase.Id, quote.Id, quote.CustomerId, quote.AnnualPremium, purchase.StartDate, now), cancellationToken);

        // ONE SQL transaction saves the purchase AND the outbox message.
        // Either both happen or neither does: no "paid but no event" or "event but no payment".
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // SQL is the source of truth. Marking the Mongo quote is best-effort: if it fails, the
        // unique index still stops a second purchase, and a retry or reconciliation job can fix the status.
        quote.MarkPurchased(now);
        await quotes.UpdateAsync(quote, cancellationToken);

        return new PurchaseResponse(purchase.Id, quote.Id, purchase.PaymentReference, "Pending");
    }
}
