namespace QuickCover.Domain.Policies;

/// <summary>An issued insurance policy. Created asynchronously by the policy worker.</summary>
public sealed class Policy
{
    private Policy() { }

    public Guid Id { get; private set; }
    public string PolicyNumber { get; private set; } = default!;
    public Guid QuoteId { get; private set; }
    public string CustomerId { get; private set; } = default!;
    public decimal AnnualPremium { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public DateTime IssuedUtc { get; private set; }

    public static Policy Issue(Guid quoteId, string customerId, decimal annualPremium, DateOnly startDate, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        PolicyNumber = $"QC-{nowUtc:yyMMdd}-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
        QuoteId = quoteId,
        CustomerId = customerId,
        AnnualPremium = annualPremium,
        StartDate = startDate,
        EndDate = startDate.AddYears(1).AddDays(-1),
        IssuedUtc = nowUtc
    };
}
