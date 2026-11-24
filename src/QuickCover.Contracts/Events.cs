namespace QuickCover.Contracts;

/// <summary>Published by the Quote API (via the outbox) when a customer pays for a quote.</summary>
public sealed record QuotePurchased(
    Guid PurchaseId,
    Guid QuoteId,
    string CustomerId,
    decimal AnnualPremium,
    DateOnly StartDate,
    DateTime PurchasedUtc);

/// <summary>Published by the policy worker once the policy exists. The docs worker (step 2) consumes this.</summary>
public sealed record PolicyIssued(
    Guid PolicyId,
    string PolicyNumber,
    Guid QuoteId,
    string CustomerId,
    DateTime IssuedUtc);
