namespace QuickCover.Contracts;

/// <summary>Published by the Quote API (via the outbox) when a customer pays for a quote.</summary>
public sealed record QuotePurchased(
    Guid PurchaseId,
    Guid QuoteId,
    string CustomerId,
    decimal AnnualPremium,
    DateOnly StartDate,
    DateTime PurchasedUtc);

/// <summary>
/// Published by the policy worker once the policy exists. The docs worker consumes it to build the policy PDF.
/// It carries everything the document needs (event-carried state transfer), so the docs worker never
/// has to call back into the policy database.
/// </summary>
public sealed record PolicyIssued(
    Guid PolicyId,
    string PolicyNumber,
    Guid QuoteId,
    string CustomerId,
    decimal AnnualPremium,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime IssuedUtc);
