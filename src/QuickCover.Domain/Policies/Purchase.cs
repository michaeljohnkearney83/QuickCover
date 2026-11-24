namespace QuickCover.Domain.Policies;

/// <summary>
/// A customer has paid for a quote. Stored in SQL, which is the source of truth for money.
/// QuoteId has a unique index, so the same quote can never be bought twice.
/// </summary>
public sealed class Purchase
{
    private Purchase() { }

    public Guid Id { get; private set; }
    public Guid QuoteId { get; private set; }
    public string CustomerId { get; private set; } = default!;
    public decimal AmountPaid { get; private set; }
    public DateOnly StartDate { get; private set; }
    public string PaymentReference { get; private set; } = default!;
    public DateTime PurchasedUtc { get; private set; }

    public static Purchase Create(Guid quoteId, string customerId, decimal amount, DateOnly startDate, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        QuoteId = quoteId,
        CustomerId = customerId,
        AmountPaid = amount,
        StartDate = startDate,
        // Stand-in for a real payment provider reference.
        PaymentReference = $"PAY-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
        PurchasedUtc = nowUtc
    };
}
