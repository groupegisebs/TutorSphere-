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
    public void Parent_homework_detail_is_read_only_and_has_no_sensitive_fields()
    {
        var names = typeof(ParentHomeworkDetailDto).GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("DateOfBirth", names);
        Assert.DoesNotContain("Email", names);
        Assert.DoesNotContain("AccessCode", names);
        Assert.DoesNotContain("SubmissionNotes", names);
        Assert.Contains("CanRemind", names);
        Assert.Contains("Instructions", names);
        Assert.Contains("Feedback", names);
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

    [Fact]
    public void Progress_contract_does_not_expose_sensitive_fields_or_sibling_scores()
    {
        var names = typeof(ParentProgressReportDto).GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(ParentProgressChildDto).GetProperties().Select(p => p.Name))
            .Concat(typeof(ParentProgressObservationDto).GetProperties().Select(p => p.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] forbidden =
        [
            "DateOfBirth",
            "Email",
            "Phone",
            "LoginAccessCode",
            "AccessCode",
            "Address",
            "Sibling",
            "Brother",
            "Sister"
        ];

        foreach (var name in forbidden)
            Assert.DoesNotContain(name, names);

        Assert.Contains("HasGroupBenchmark", names);
        Assert.Contains("GroupAveragePercent", typeof(ParentProgressPointDto).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("Children", typeof(ParentProgressReportDto).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public void Progress_report_json_has_no_contact_pii()
    {
        var dto = new ParentProgressReportDto(
            Guid.NewGuid(),
            "Junior",
            "Bediga",
            "Primaire 1",
            78,
            6,
            15.6m,
            0.8m,
            92,
            3,
            18,
            24,
            2,
            true,
            [new ParentProgressPointDto(new DateTime(2024, 11, 1), 75, 70)],
            [],
            [],
            [],
            [],
            3,
            5,
            []);

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.DoesNotContain("dateOfBirth", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessCode", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hasGroupBenchmark", json);
        Assert.Contains("groupAveragePercent", json);
    }
}
