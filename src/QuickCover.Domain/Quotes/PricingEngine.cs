namespace QuickCover.Domain.Quotes;

/// <summary>Pure function: same inputs, same price. Easy to unit test.</summary>
public static class PricingEngine
{
    public static decimal CalculateAnnualPremium(QuoteRequest request, RatingFactors factors)
    {
        var premium = factors.BaseRate + request.VehicleValue * factors.VehicleValueRate;

        if (request.DriverAge < 25)
            premium *= factors.YoungDriverLoading;

        if (request.YearsLicensed < 3)
            premium *= factors.NewLicenceLoading;

        premium *= 1m + factors.ClaimLoadingPerClaim * request.ClaimsLast5Years;

        if (request.Use == VehicleUse.PublicHire)
            premium *= factors.PublicHireLoading;

        var area = request.PostcodeArea.Trim().ToUpperInvariant();
        premium *= factors.AreaMultipliers.TryGetValue(area, out var multiplier)
            ? multiplier
            : factors.DefaultAreaMultiplier;

        return decimal.Round(premium, 2, MidpointRounding.AwayFromZero);
    }
}
