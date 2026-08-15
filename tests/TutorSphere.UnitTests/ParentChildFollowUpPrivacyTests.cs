using System.Text.Json;
using TutorSphere.Application.DTOs.Parents;

namespace TutorSphere.UnitTests;

public class ParentChildFollowUpPrivacyTests
{
    [Fact]
    public void Follow_up_contract_does_not_expose_sensitive_child_fields()
    {
        var names = typeof(ParentChildFollowUpDto).GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] forbidden =
        [
            "DateOfBirth",
            "Email",
            "Phone",
            "LoginAccessCode",
            "AccessCode",
            "Address",
            "Street",
            "PostalCode",
            "Notes"
        ];

        foreach (var name in forbidden)
            Assert.DoesNotContain(name, names);
    }

    [Fact]
    public void Calendar_contract_does_not_expose_sensitive_fields()
    {
        var names = typeof(ParentCalendarEventDto).GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(ParentCalendarChildDto).GetProperties().Select(p => p.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] forbidden =
        [
            "DateOfBirth",
            "Email",
            "Phone",
            "LoginAccessCode",
            "AccessCode",
            "Address"
        ];

        foreach (var name in forbidden)
            Assert.DoesNotContain(name, names);
    }

    [Fact]
    public void Follow_up_json_has_no_plaintext_access_code_or_contact_pii()
    {
        var dto = new ParentChildFollowUpDto(
            Guid.NewGuid(),
            78,
            6,
            2,
            null,
            92,
            [],
            [],
            [],
            [],
            true);

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.DoesNotContain("dateOfBirth", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessCode", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("address", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"hasLoginAccess\":true", json);
    }
}
