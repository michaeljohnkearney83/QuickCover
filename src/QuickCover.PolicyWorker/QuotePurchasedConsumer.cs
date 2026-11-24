using MassTransit;
using Microsoft.EntityFrameworkCore;
using QuickCover.Contracts;
using QuickCover.Domain.Policies;
using QuickCover.Infrastructure.Sql;

namespace QuickCover.PolicyWorker;

/// <summary>
/// Turns a purchase into an issued policy.
///
/// Idempotency, three layers deep:
///  1. MassTransit inbox: a message with the same MessageId is only processed once.
///  2. The check below: if a policy already exists for this quote, do nothing.
///  3. Unique index on Policies.QuoteId: the database refuses a duplicate even if 1 and 2 are bypassed.
/// </summary>
public sealed class QuotePurchasedConsumer(
    QuickCoverDbContext db,
    TimeProvider time,
    ILogger<QuotePurchasedConsumer> logger) : IConsumer<QuotePurchased>
{
    public async Task Consume(ConsumeContext<QuotePurchased> context)
    {
        var message = context.Message;

        if (await db.Policies.AnyAsync(p => p.QuoteId == message.QuoteId, context.CancellationToken))
        {
            logger.LogInformation("Policy already exists for quote {QuoteId}; skipping", message.QuoteId);
            return;
        }

        var policy = Policy.Issue(
            message.QuoteId,
            message.CustomerId,
            message.AnnualPremium,
            message.StartDate,
            time.GetUtcNow().UtcDateTime);

        db.Policies.Add(policy);

        // Goes into the outbox with the policy row; sent only after the transaction commits.
        await context.Publish(new PolicyIssued(
            policy.Id, policy.PolicyNumber, policy.QuoteId, policy.CustomerId,
            policy.AnnualPremium, policy.StartDate, policy.EndDate, policy.IssuedUtc));

        await db.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Issued policy {PolicyNumber} for quote {QuoteId}", policy.PolicyNumber, policy.QuoteId);
    }
}
