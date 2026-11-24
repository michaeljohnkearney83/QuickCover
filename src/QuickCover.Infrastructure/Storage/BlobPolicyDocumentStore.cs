using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using QuickCover.Application.Common;

namespace QuickCover.Infrastructure.Storage;

/// <summary>
/// Policy PDFs in a private blob container, one blob per policy: policies/{policyId}.pdf.
/// The container is never public. Customers get documents through the API, which checks ownership first.
/// </summary>
internal sealed class BlobPolicyDocumentStore(BlobServiceClient blobService, string containerName) : IPolicyDocumentStore
{
    private readonly BlobContainerClient _container = blobService.GetBlobContainerClient(containerName);
    private volatile bool _containerReady;

    public async Task SaveAsync(Guid policyId, Stream pdf, CancellationToken ct)
    {
        if (!_containerReady)
        {
            await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            _containerReady = true;
        }

        await Blob(policyId).UploadAsync(pdf, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" }
        }, ct);
    }

    public async Task<Stream?> OpenReadAsync(Guid policyId, CancellationToken ct)
    {
        try
        {
            var download = await Blob(policyId).DownloadStreamingAsync(cancellationToken: ct);
            return download.Value.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Blob or container missing: the docs worker has not produced it yet.
            return null;
        }
    }

    private BlobClient Blob(Guid policyId) => _container.GetBlobClient($"policies/{policyId}.pdf");
}
