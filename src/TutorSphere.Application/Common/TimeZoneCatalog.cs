namespace TutorSphere.Application.Common;

public static class TimeZoneCatalog
{
    public static readonly IReadOnlyList<(string Id, string Label)> All =
    [
        ("Africa/Abidjan", "Abidjan"),
        ("Africa/Douala", "Douala"),
        ("Africa/Dakar", "Dakar"),
        ("Africa/Libreville", "Libreville"),
        ("Africa/Kinshasa", "Kinshasa"),
        ("Africa/Brazzaville", "Brazzaville"),
        ("Africa/Lagos", "Lagos"),
        ("Africa/Casablanca", "Casablanca"),
        ("Africa/Tunis", "Tunis"),
        ("Africa/Algiers", "Alger"),
        ("America/Toronto", "Toronto"),
        ("America/Montreal", "Montréal"),
        ("America/Vancouver", "Vancouver"),
        ("America/New_York", "New York"),
        ("America/Chicago", "Chicago"),
        ("America/Denver", "Denver"),
        ("America/Los_Angeles", "Los Angeles"),
        ("Europe/Paris", "Paris"),
        ("Europe/Brussels", "Bruxelles"),
        ("Europe/Zurich", "Zurich"),
        ("Europe/London", "Londres"),
        ("Europe/Berlin", "Berlin"),
        ("UTC", "UTC")
    ];

    public static string DefaultForCountry(string? iso2) => (iso2 ?? "").Trim().ToUpperInvariant() switch
    {
        "CI" => "Africa/Abidjan",
        "CM" => "Africa/Douala",
        "SN" => "Africa/Dakar",
        "GA" => "Africa/Libreville",
        "CD" => "Africa/Kinshasa",
        "CG" => "Africa/Brazzaville",
        "NG" => "Africa/Lagos",
        "MA" => "Africa/Casablanca",
        "TN" => "Africa/Tunis",
        "DZ" => "Africa/Algiers",
        "CA" => "America/Toronto",
        "US" => "America/New_York",
        "FR" => "Europe/Paris",
        "BE" => "Europe/Brussels",
        "CH" => "Europe/Zurich",
        "GB" => "Europe/London",
        "DE" => "Europe/Berlin",
        _ => "America/Montreal"
    };

    public static string FormatOption(string id)
    {
        var label = All.FirstOrDefault(z => string.Equals(z.Id, id, StringComparison.OrdinalIgnoreCase)).Label;
        var name = string.IsNullOrWhiteSpace(label) ? id : label;
        return $"{id} ({UtcOffsetLabel(id)}) — {name}";
    }

    public static string UtcOffsetLabel(string id)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(id);
            var offset = tz.GetUtcOffset(DateTime.UtcNow);
            var sign = offset < TimeSpan.Zero ? "−" : "+";
            var abs = offset.Duration();
            return $"UTC{sign}{abs.Hours:D2}:{abs.Minutes:D2}";
        }
        catch (TimeZoneNotFoundException)
        {
            return "UTC";
        }
        catch (InvalidTimeZoneException)
        {
            return "UTC";
        }
    }

    public static string Normalize(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "America/Montreal";
        var trimmed = id.Trim();
        return All.Any(z => string.Equals(z.Id, trimmed, StringComparison.OrdinalIgnoreCase))
            ? trimmed
            : trimmed;
    }
}
