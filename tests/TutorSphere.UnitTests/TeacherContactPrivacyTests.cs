using TutorSphere.Application.Common;

namespace TutorSphere.UnitTests;

public class TeacherContactPrivacyTests
{
    [Fact]
    public void RedactFromPublicText_removes_email_and_phone()
    {
        var input = "Écrivez-moi à jean.dupont@ecole.ca ou au +1 (514) 555-1234 merci.";
        var result = TeacherContactPrivacy.RedactFromPublicText(input);

        Assert.DoesNotContain("@", result);
        Assert.DoesNotContain("514", result);
        Assert.Contains(TeacherContactPrivacy.RedactedPlaceholder, result);
    }

    [Fact]
    public void MaskEmail_hides_local_part()
    {
        Assert.Equal("j••••t@ecole.ca", TeacherContactPrivacy.MaskEmail("jean.dupont@ecole.ca"));
    }

    [Fact]
    public void StripTeacherContactKeys_removes_forbidden_keys()
    {
        var data = new Dictionary<string, string>
        {
            ["TutorName"] = "Marie",
            ["TutorEmail"] = "marie@secret.ca",
            ["TeacherPhone"] = "5145550000",
            ["Subject"] = "Maths"
        };

        TeacherContactPrivacy.StripTeacherContactKeys(data);

        Assert.Equal("Marie", data["TutorName"]);
        Assert.Equal("Maths", data["Subject"]);
        Assert.False(data.ContainsKey("TutorEmail"));
        Assert.False(data.ContainsKey("TeacherPhone"));
    }
}
