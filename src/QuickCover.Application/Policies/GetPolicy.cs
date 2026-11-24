using Mediator;
using QuickCover.Application.Common;

namespace QuickCover.Application.Policies;

/// <summary>Returns null while the policy worker is still issuing the policy.</summary>
public sealed record GetPolicyForQuoteQuery(Guid QuoteId, string CustomerId) : IQuery<PolicySummary?>;

public sealed class GetPolicyForQuoteHandler(IPolicyReadRepository policies)
    : IQueryHandler<GetPolicyForQuoteQuery, PolicySummary?>
{
    public async ValueTask<PolicySummary?> Handle(GetPolicyForQuoteQuery query, CancellationToken cancellationToken) =>
        await policies.GetByQuoteIdAsync(query.QuoteId, query.CustomerId, cancellationToken);
}
