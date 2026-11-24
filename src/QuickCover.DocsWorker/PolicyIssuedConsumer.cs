using MassTransit;
using QuickCover.Application.Common;
using QuickCover.Contracts;

namespace QuickCover.DocsWorker;

/// <summary>
/// Builds the policy schedule PDF and stores it in blob storage.
///
/// Idempotent by design: the blob name is derived from the policy id, so a redelivered message
/// just overwrites the same file with an identical document.
/// </summary>
public sealed class PolicyIssuedConsumer(
    PolicyDocumentRenderer renderer,
    IPolicyDocumentStore documents,
    ILogger<PolicyIssuedConsumer> logger) : IConsumer<PolicyIssued>
{
    public async Task Consume(ConsumeContext<PolicyIssued> context)
    {
        var policy = context.Message;

        var pdf = renderer.Render(policy);
        using var stream = new MemoryStream(pdf, writable: false);
        await documents.SaveAsync(policy.PolicyId, stream, context.CancellationToken);

        logger.LogInformation("Stored policy document for {PolicyNumber} ({Bytes} bytes)", policy.PolicyNumber, pdf.Length);
    }
}
