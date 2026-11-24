using QuickCover.Domain;
using QuickCover.Domain.Quotes;

namespace QuickCover.UnitTests;

public class QuoteTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly QuoteRequest Request = new(40, 20, 0, 15_000m, VehicleUse.PrivateHire, "L");

    [Fact]
    public void New_quote_is_open_and_expires_after_30_days()
    {
        var quote = Quote.Create("cust-1", Request, 1234.56m, Now);

        Assert.Equal(QuoteStatus.Open, quote.Status);
        Assert.Equal(Now.AddDays(30), quote.ExpiresUtc);
    }

    [Fact]
    public void Quote_can_be_purchased_once()
    {
        var quote = Quote.Create("cust-1", Request, 1234.56m, Now);

        quote.MarkPurchased(Now.AddHours(1));

        Assert.Equal(QuoteStatus.Purchased, quote.Status);
        Assert.Throws<DomainException>(() => quote.MarkPurchased(Now.AddHours(2)));
    }

    [Fact]
    public void Expired_quote_cannot_be_purchased()
    {
        var quote = Quote.Create("cust-1", Request, 1234.56m, Now);

        Assert.Throws<DomainException>(() => quote.EnsureCanPurchase(Now.AddDays(31)));
    }
}
