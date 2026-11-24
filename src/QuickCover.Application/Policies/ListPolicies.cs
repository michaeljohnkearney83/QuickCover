using Mediator;
using QuickCover.Application.Common;

namespace QuickCover.Application.Policies;

/// <summary>The signed-in customer's issued policies, newest first.</summary>
public sealed record ListPoliciesQuery(string CustomerId) : IQuery<IReadOnlyList<PolicySummary>>, IValidatable
{
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(CustomerId))
            yield return "Customer id is required.";
    }
}

public sealed class ListPoliciesHandler(IPolicyReadRepository policies)
    : IQueryHandler<ListPoliciesQuery, IReadOnlyList<PolicySummary>>
{
    public async ValueTask<IReadOnlyList<PolicySummary>> Handle(ListPoliciesQuery query, CancellationToken cancellationToken) =>
        await policies.ListForCustomerAsync(query.CustomerId, cancellationToken);
}
