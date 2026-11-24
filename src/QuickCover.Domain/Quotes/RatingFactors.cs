namespace QuickCover.Domain.Quotes;

/// <summary>
/// The pricing table. In a real insurer this comes from the pricing team and changes
/// rarely, which makes it a good candidate for caching (see CachedRatingFactorProvider).
/// </summary>
public sealed record RatingFactors(
    decimal BaseRate,
    decimal VehicleValueRate,
    decimal YoungDriverLoading,
    decimal NewLicenceLoading,
    decimal ClaimLoadingPerClaim,
    decimal PublicHireLoading,
    decimal DefaultAreaMultiplier,
    Dictionary<string, decimal> AreaMultipliers);
