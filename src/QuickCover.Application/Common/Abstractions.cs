using QuickCover.Domain.Policies;
using QuickCover.Domain.Quotes;

namespace QuickCover.Application.Common;

// Ports: the Application layer says WHAT it needs; Infrastructure decides HOW.
// That's what lets us swap RabbitMQ for Service Bus, or Mongo for DocumentDB, without touching handlers.

public interface IQuoteRepository
{
    Task AddAsync(Quote quote, CancellationToken ct);
    Task<Quote?> GetAsync(Guid id, CancellationToken ct);
    Task UpdateAsync(Quote quote, CancellationToken ct);
}

public interface IRatingFactorProvider
{
    Task<RatingFactors> GetAsync(CancellationToken ct);
}

public interface IPurchaseRepository
{
    void Add(Purchase purchase);
    Task<bool> ExistsForQuoteAsync(Guid quoteId, CancellationToken ct);
}

public interface IPolicyReadRepository
{
    Task<PolicySummary?> GetByQuoteIdAsync(Guid quoteId, string customerId, CancellationToken ct);
}

public interface IEventPublisher
{
    /// <summary>Queues an integration event. It is only sent once the unit of work commits (outbox).</summary>
    Task PublishAsync<T>(T message, CancellationToken ct) where T : class;
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Commands that can check their own input before the handler runs.</summary>
public interface IValidatable
{
    IEnumerable<string> Validate();
}

public sealed record PolicySummary(
    Guid PolicyId,
    string PolicyNumber,
    Guid QuoteId,
    decimal AnnualPremium,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime IssuedUtc);
