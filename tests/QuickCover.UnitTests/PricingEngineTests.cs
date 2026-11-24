using QuickCover.Domain.Quotes;

namespace QuickCover.UnitTests;

public class PricingEngineTests
{
    // Simple numbers so each expected value can be worked out by hand.
    private static readonly RatingFactors Factors = new(
        BaseRate: 1000m,
        VehicleValueRate: 0.01m,
        YoungDriverLoading: 1.5m,
        NewLicenceLoading: 1.2m,
        ClaimLoadingPerClaim: 0.25m,
        PublicHireLoading: 1.1m,
        DefaultAreaMultiplier: 1.0m,
        AreaMultipliers: new Dictionary<string, decimal> { ["L"] = 2.0m });

    private static QuoteRequest Experienced(
        int age = 40, int yearsLicensed = 20, int claims = 0,
        decimal value = 10_000m, VehicleUse use = VehicleUse.PrivateHire, string area = "CH") =>
        new(age, yearsLicensed, claims, value, use, area);

    [Fact]
    public void Experienced_driver_with_no_loadings_pays_base_plus_vehicle_rate()
    {
        // 1000 + 10,000 * 0.01 = 1100
        Assert.Equal(1100m, PricingEngine.CalculateAnnualPremium(Experienced(), Factors));
    }

    [Fact]
    public void Young_driver_loading_applies_under_25()
    {
        // 1100 * 1.5 = 1650
        var premium = PricingEngine.CalculateAnnualPremium(Experienced(age: 24, yearsLicensed: 6), Factors);
        Assert.Equal(1650m, premium);
    }

    [Fact]
    public void Each_claim_adds_its_loading()
    {
        // 1100 * (1 + 0.25 * 2) = 1650
        Assert.Equal(1650m, PricingEngine.CalculateAnnualPremium(Experienced(claims: 2), Factors));
    }

    [Fact]
    public void Public_hire_and_area_multipliers_combine()
    {
        // 1100 * 1.1 * 2.0 = 2420
        var premium = PricingEngine.CalculateAnnualPremium(
            Experienced(use: VehicleUse.PublicHire, area: "l"), Factors); // area match is case-insensitive
        Assert.Equal(2420m, premium);
    }

    [Fact]
    public void Unknown_area_uses_default_multiplier()
    {
        Assert.Equal(1100m, PricingEngine.CalculateAnnualPremium(Experienced(area: "ZZ"), Factors));
    }

    [Theory]
    [InlineData(20, 2, 0, 10_000, "L")]  // too young
    [InlineData(30, 20, 0, 10_000, "L")] // licensed longer than possible
    [InlineData(30, 5, 11, 10_000, "L")] // too many claims
    [InlineData(30, 5, 0, 500, "L")]     // vehicle value too low
    [InlineData(30, 5, 0, 10_000, "L1")] // bad postcode area
    public void Invalid_requests_produce_errors(int age, int years, int claims, int value, string area)
    {
        var request = new QuoteRequest(age, years, claims, value, VehicleUse.PrivateHire, area);
        Assert.NotEmpty(request.Validate());
    }
}
