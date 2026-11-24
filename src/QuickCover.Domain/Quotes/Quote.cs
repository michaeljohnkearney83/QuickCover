namespace QuickCover.Domain.Quotes;

public enum QuoteStatus
{
    Open,
    Purchased
}

/// <summary>A priced quote. Stored in MongoDB (DocumentDB in Azure).</summary>
public sealed class Quote
{
    public static readonly TimeSpan ValidFor = TimeSpan.FromDays(30);

    private Quote() { }

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = default!;
    public QuoteRequest Request { get; private set; } = default!;
    public decimal AnnualPremium { get; private set; }
    public QuoteStatus Status { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime ExpiresUtc { get; private set; }
    public DateTime? PurchasedUtc { get; private set; }

    public static Quote Create(string customerId, QuoteRequest request, decimal annualPremium, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        Request = request,
        AnnualPremium = annualPremium,
        Status = QuoteStatus.Open,
        CreatedUtc = nowUtc,
        ExpiresUtc = nowUtc.Add(ValidFor)
    };

    /// <summary>Rebuilds a quote from storage without re-running creation rules.</summary>
    public static Quote Rehydrate(
        Guid id, string customerId, QuoteRequest request, decimal annualPremium,
        QuoteStatus status, DateTime createdUtc, DateTime expiresUtc, DateTime? purchasedUtc) => new()
    {
        Id = id,
        CustomerId = customerId,
        Request = request,
        AnnualPremium = annualPremium,
        Status = status,
        CreatedUtc = createdUtc,
        ExpiresUtc = expiresUtc,
        PurchasedUtc = purchasedUtc
    };

    public void EnsureCanPurchase(DateTime nowUtc)
    {
        if (Status != QuoteStatus.Open)
            throw new DomainException("This quote has already been purchased.");

        if (nowUtc > ExpiresUtc)
            throw new DomainException("This quote has expired. Please get a new quote.");
    }

    public void MarkPurchased(DateTime nowUtc)
    {
        EnsureCanPurchase(nowUtc);
        Status = QuoteStatus.Purchased;
        PurchasedUtc = nowUtc;
    }
}
