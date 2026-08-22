using TutorSphere.Application.Common;

namespace TutorSphere.UnitTests;

public class AddressCoherenceTests
{
    [Fact]
    public void Germany_rejects_quebec_canadian_postal()
    {
        var issue = AddressCoherence.Evaluate(
            "DE", "Québec", "G3K 0P6", "1200 rue Edison", "4185763462");

        Assert.NotNull(issue);
        Assert.Equal(AddressCoherenceKind.PostalOtherCountry, issue!.Kind);
        Assert.Equal("CA", issue.OtherCountryIso);
        Assert.Contains("Canada", AddressCoherence.FormatMessage(issue), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Allemagne", AddressCoherence.FormatMessage(issue), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Germany_rejects_quebec_city_when_postal_empty()
    {
        var issue = AddressCoherence.Evaluate("DE", "Québec", null);
        Assert.NotNull(issue);
        Assert.Equal(AddressCoherenceKind.CityOtherCountry, issue!.Kind);
        Assert.Equal("CA", issue.OtherCountryIso);
    }

    [Fact]
    public void Germany_rejects_french_street_name()
    {
        var issue = AddressCoherence.Evaluate("DE", "Berlin", "10115", "1200 rue Edison");
        Assert.NotNull(issue);
        Assert.Equal(AddressCoherenceKind.StreetOtherCountry, issue!.Kind);
    }

    [Fact]
    public void Germany_rejects_quebec_area_code()
    {
        var issue = AddressCoherence.Evaluate("DE", "Berlin", "10115", "Alexanderplatz 1", "4185763462");
        Assert.NotNull(issue);
        Assert.Equal(AddressCoherenceKind.PhoneOtherCountry, issue!.Kind);
        Assert.Equal("CA", issue.OtherCountryIso);
    }

    [Fact]
    public void Canada_accepts_quebec_address()
    {
        var issue = AddressCoherence.Evaluate(
            "CA", "Québec", "G3K 0P6", "1200 rue Edison", "4185763462");
        Assert.Null(issue);
    }

    [Fact]
    public void Germany_accepts_berlin_address()
    {
        var issue = AddressCoherence.Evaluate("DE", "Berlin", "10115", "Alexanderstraße 1", "030123456");
        Assert.Null(issue);
    }

    [Fact]
    public void IvoryCoast_accepts_abidjan_without_postal()
    {
        var issue = AddressCoherence.Evaluate("CI", "Abidjan", null, "Cocody");
        Assert.Null(issue);
    }

    [Fact]
    public void IvoryCoast_rejects_canadian_postal()
    {
        var issue = AddressCoherence.Evaluate("CI", "Abidjan", "H2X 1Y4");
        Assert.NotNull(issue);
        Assert.Equal(AddressCoherenceKind.PostalOtherCountry, issue!.Kind);
        Assert.Equal("CA", issue.OtherCountryIso);
    }

    [Fact]
    public void Postal_example_follows_country()
    {
        Assert.Equal("G3K 0P6", AddressCoherence.PostalExample("CA"));
        Assert.Equal("10115", AddressCoherence.PostalExample("DE"));
        Assert.Null(AddressCoherence.PostalExample("CI"));
    }
}
