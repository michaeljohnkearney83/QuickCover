using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuickCover.Application.Common;
using QuickCover.Domain.Policies;

namespace QuickCover.Infrastructure.Sql;

internal sealed class EfPurchaseRepository(QuickCoverDbContext db) : IPurchaseRepository
{
    public void Add(Purchase purchase) => db.Purchases.Add(purchase);

    public Task<bool> ExistsForQuoteAsync(Guid quoteId, CancellationToken ct) =>
        db.Purchases.AnyAsync(p => p.QuoteId == quoteId, ct);
}

internal sealed class EfPolicyReadRepository(QuickCoverDbContext db) : IPolicyReadRepository
{
    public Task<PolicySummary?> GetByQuoteIdAsync(Guid quoteId, string customerId, CancellationToken ct) =>
        db.Policies
            .AsNoTracking()
            .Where(p => p.QuoteId == quoteId && p.CustomerId == customerId)
            .Select(p => new PolicySummary(
                p.Id, p.PolicyNumber, p.QuoteId, p.AnnualPremium, p.StartDate, p.EndDate, p.IssuedUtc))
            .FirstOrDefaultAsync(ct);

    public Task<PolicySummary?> GetAsync(Guid policyId, string customerId, CancellationToken ct) =>
        db.Policies
            .AsNoTracking()
            .Where(p => p.Id == policyId && p.CustomerId == customerId)
            .Select(p => new PolicySummary(
                p.Id, p.PolicyNumber, p.QuoteId, p.AnnualPremium, p.StartDate, p.EndDate, p.IssuedUtc))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<PolicySummary>> ListForCustomerAsync(string customerId, CancellationToken ct) =>
        await db.Policies
            .AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.IssuedUtc)
            .Select(p => new PolicySummary(
                p.Id, p.PolicyNumber, p.QuoteId, p.AnnualPremium, p.StartDate, p.EndDate, p.IssuedUtc))
            .ToListAsync(ct);
}

internal sealed class EfUnitOfWork(QuickCoverDbContext db) : IUnitOfWork
{
    // SQL Server error numbers for a unique index/constraint violation.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            // Two requests raced to buy the same quote; the database stopped the second one.
            throw new ConflictException("This quote has already been purchased.");
        }
    }
}
