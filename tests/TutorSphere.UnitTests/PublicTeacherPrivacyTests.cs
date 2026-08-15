using System.Text.Json;
using TutorSphere.Application.Common;
using TutorSphere.Application.DTOs.Branding;

namespace TutorSphere.UnitTests;

public class PublicTeacherPrivacyTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Public_display_name_uses_first_name_and_last_initial()
    {
        Assert.Equal("Jean B.", TeacherPublicName.Format("Jean", "Bediga"));
        Assert.Equal("Marie D.", TeacherPublicName.Format("Marie", "Dupont"));
        Assert.Equal("Jean", TeacherPublicName.Format("Jean", null));
        Assert.Equal("JB", TeacherPublicName.Initials("Jean", "Bediga"));
    }

    [Fact]
    public void General_location_drops_street_and_postal_code()
    {
        Assert.Null(TeacherPublicName.GeneralLocation("12 rue des Écoles", "75001"));
        Assert.Equal("Abidjan, Côte d'Ivoire", TeacherPublicName.GeneralLocation("Abidjan", "Côte d'Ivoire"));
    }

    [Theory]
    [InlineData("photo.jpg", "logo.png", TeacherPublicPhotoKind.TeacherPhoto, false)]
    [InlineData(null, "logo.png", TeacherPublicPhotoKind.GroupLogo, true)]
    [InlineData("", "", TeacherPublicPhotoKind.Initials, false)]
    public void Photo_priority_is_teacher_then_group_then_initials(
        string? teacherPhoto,
        string? groupLogo,
        TeacherPublicPhotoKind expectedKind,
        bool expectedFallback)
    {
        var photo = TeacherPublicPhotoResolver.Resolve(teacherPhoto, groupLogo, "JB");
        Assert.Equal(expectedKind, photo.Kind);
        Assert.Equal(expectedFallback, photo.IsGroupLogoFallback);
        if (expectedKind == TeacherPublicPhotoKind.TeacherPhoto)
            Assert.Equal("photo.jpg", photo.Url);
        if (expectedKind == TeacherPublicPhotoKind.GroupLogo)
            Assert.Equal("logo.png", photo.Url);
        if (expectedKind == TeacherPublicPhotoKind.Initials)
            Assert.Null(photo.Url);
    }

    [Fact]
    public void Teacher_photo_is_never_replaced_by_group_logo()
    {
        var photo = TeacherPublicPhotoResolver.Resolve("/uploads/teacher.png", "/uploads/group.png", "JB");
        Assert.Equal(TeacherPublicPhotoKind.TeacherPhoto, photo.Kind);
        Assert.Equal("/uploads/teacher.png", photo.Url);
        Assert.False(photo.IsGroupLogoFallback);
    }

    [Fact]
    public void Public_profile_json_has_no_personal_fields()
    {
        var dto = new TeacherPublicProfileDto
        {
            Slug = "jean-b",
            DisplayName = "Jean B.",
            GivenName = "Jean",
            PublicInitials = "JB",
            Location = "Abidjan, Côte d'Ivoire",
            City = "Abidjan",
            Country = "Côte d'Ivoire",
            Language = "fr",
            Currency = "XOF",
            PhotoUrl = "/uploads/teacher.webp",
            PhotoKind = "teacherPhoto",
            ShortBio = "Cours de maths.",
            ExpertGroupName = "Groupe CI"
        };

        var json = JsonSerializer.Serialize(dto, JsonOpts);
        var hits = TeacherPublicPiiGuard.FindForbiddenProperties(json);
        Assert.Empty(hits);
        Assert.DoesNotContain("Bediga", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"displayName\":\"Jean B.\"", json);
    }

    [Fact]
    public void Public_text_redacts_address_birth_date_email_and_phone()
    {
        var input = "Né le 12/03/1990, j'habite 18 rue des Lilas, H2X 1Y4. jean@ecole.ca / +1 514 555 1234";
        var result = TeacherContactPrivacy.RedactFromPublicText(input)!;
        Assert.DoesNotContain("1990", result);
        Assert.DoesNotContain("Lilas", result);
        Assert.DoesNotContain("H2X", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", result);
        Assert.DoesNotContain("514", result);
    }

    [Fact]
    public void Search_result_json_has_no_personal_fields()
    {
        var dto = new TutorSphere.Application.DTOs.Search.TutorSearchResultDto(
            Guid.NewGuid(),
            "Jean B.",
            "jean-b",
            "Abidjan",
            "CI",
            "Cours de maths",
            "fr",
            "XOF",
            15,
            20,
            ["Maths"],
            ["En ligne"],
            4.8m,
            "/uploads/photo.webp",
            ExpertGroupName: "Groupe CI",
            OfferTitle: "Maths collège",
            PhotoKind: "teacherPhoto");

        var json = JsonSerializer.Serialize(dto, JsonOpts);
        Assert.Empty(TeacherPublicPiiGuard.FindForbiddenProperties(json));
        Assert.DoesNotContain("birthDate", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("postalCode", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ownerUserId", json, StringComparison.OrdinalIgnoreCase);
    }
}
