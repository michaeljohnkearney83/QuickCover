using System.Text;
using QuickCover.Contracts;
using QuickCover.DocsWorker;
using QuestPDF.Infrastructure;

namespace QuickCover.UnitTests;

public class PolicyDocumentRendererTests
{
    private static readonly PolicyIssued Policy = new(
        Guid.NewGuid(), "QC-261124-ABCDEF", Guid.NewGuid(), "cust-1",
        1944.00m, new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 30),
        new DateTime(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc));

    static PolicyDocumentRendererTests() => QuestPDF.Settings.License = LicenseType.Community;

    [Fact]
    public void Renders_a_pdf()
    {
        var renderer = new PolicyDocumentRenderer(new FixedClock(Policy.IssuedUtc));

        var pdf = renderer.Render(Policy);

        Assert.True(pdf.Length > 1_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}

/// <summary>A clock stuck at one instant, so the rendered document is deterministic.</summary>
internal sealed class FixedClock(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
}
