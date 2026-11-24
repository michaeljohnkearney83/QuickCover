using System.Text.RegularExpressions;

namespace QuickCover.Domain.Quotes;

public enum VehicleUse
{
    PrivateHire,
    PublicHire
}

/// <summary>The rating inputs a taxi driver gives us to get a price.</summary>
public sealed record QuoteRequest(
    int DriverAge,
    int YearsLicensed,
    int ClaimsLast5Years,
    decimal VehicleValue,
    VehicleUse Use,
    string PostcodeArea)
{
    private static readonly Regex PostcodeAreaPattern = new("^[A-Za-z]{1,2}$", RegexOptions.Compiled);

    public IEnumerable<string> Validate()
    {
        if (DriverAge is < 21 or > 80)
            yield return "Driver age must be between 21 and 80.";

        if (YearsLicensed < 0 || YearsLicensed > DriverAge - 17)
            yield return "Years licensed must be between 0 and (age - 17).";

        if (ClaimsLast5Years is < 0 or > 10)
            yield return "Claims in the last 5 years must be between 0 and 10.";

        if (VehicleValue is < 1_000m or > 150_000m)
            yield return "Vehicle value must be between £1,000 and £150,000.";

        if (string.IsNullOrWhiteSpace(PostcodeArea) || !PostcodeAreaPattern.IsMatch(PostcodeArea))
            yield return "Postcode area must be 1-2 letters, e.g. 'L' or 'CH'.";
    }
}
