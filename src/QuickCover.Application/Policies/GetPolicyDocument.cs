using Mediator;
using QuickCover.Application.Common;

namespace QuickCover.Application.Policies;

public sealed record GetPolicyDocumentQuery(Guid PolicyId, string CustomerId) : IQuery<PolicyDocument>;

public sealed record PolicyDocument(string FileName, Stream Content);

public sealed class GetPolicyDocumentHandler(IPolicyReadRepository policies, IPolicyDocumentStore documents)
    : IQueryHandler<GetPolicyDocumentQuery, PolicyDocument>
{
    public async ValueTask<PolicyDocument> Handle(GetPolicyDocumentQuery query, CancellationToken cancellationToken)
    {
        // Ownership check first: the blob store knows nothing about customers.
        var policy = await policies.GetAsync(query.PolicyId, query.CustomerId, cancellationToken)
                     ?? throw new NotFoundException($"Policy {query.PolicyId} was not found.");

        var content = await documents.OpenReadAsync(policy.PolicyId, cancellationToken)
                      ?? throw new NotFoundException("Your policy document is still being prepared. Try again in a moment.");

        return new PolicyDocument($"{policy.PolicyNumber}.pdf", content);
    }
}
