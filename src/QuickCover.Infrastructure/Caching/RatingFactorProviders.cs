using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using QuickCover.Application.Common;
using QuickCover.Domain.Quotes;

namespace QuickCover.Infrastructure.Caching;

/// <summary>Where the "real" rating table comes from (pretend it's a slow pricing system).</summary>
public interface IRatingTableSource
{
    Task<RatingFactors> LoadAsync(CancellationToken ct);
}

internal sealed class StaticRatingTableSource : IRatingTableSource
{
    public async Task<RatingFactors> LoadAsync(CancellationToken ct)
    {
        await Task.Delay(200, ct); // simulate a slow upstream call so the cache is visibly worth having

        return new RatingFactors(
            BaseRate: 900m,
            VehicleValueRate: 0.03m,
            YoungDriverLoading: 1.6m,
            NewLicenceLoading: 1.3m,
            ClaimLoadingPerClaim: 0.25m,
            PublicHireLoading: 1.2m,
            DefaultAreaMultiplier: 1.0m,
            AreaMultipliers: new Dictionary<string, decimal>
            {
                ["L"] = 1.35m,   // Liverpool
                ["CH"] = 1.10m,  // Chester / Wirral
                ["M"] = 1.40m,   // Manchester
                ["E"] = 1.55m,   // East London
                ["LA"] = 0.90m   // Lancaster
            });
    }
}

/// <summary>
/// Cache-aside over Redis (Azure Managed Redis in the cloud).
/// If Redis is down we fall back to the source: slower quotes, but customers can still get a price.
/// </summary>
internal sealed class CachedRatingFactorProvider(
    IDistributedCache cache,
    IRatingTableSource source,
    ILogger<CachedRatingFactorProvider> logger) : IRatingFactorProvider
{
    private const string CacheKey = "rating-factors:v1"; // bump the version when the shape changes
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
    };

    public async Task<RatingFactors> GetAsync(CancellationToken ct)
    {
        try
        {
            var cached = await cache.GetStringAsync(CacheKey, ct);
            if (cached is not null)
                return JsonSerializer.Deserialize<RatingFactors>(cached)!;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis read failed; loading rating factors from source");
            return await source.LoadAsync(ct);
        }

        var factors = await source.LoadAsync(ct);

        try
        {
            await cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(factors), CacheOptions, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis write failed; continuing without cache");
        }

        return factors;
    }
}
