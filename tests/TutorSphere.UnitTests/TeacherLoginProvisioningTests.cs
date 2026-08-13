using TutorSphere.Application.Common;

namespace TutorSphere.UnitTests;

public class TeacherLoginProvisioningTests
{
    [Fact]
    public void BuildLoginEmail_appends_four_digit_code_before_at()
    {
        var login = TeacherLoginProvisioning.BuildLoginEmail("contact@tutorax.com", 4821);
        Assert.Equal("contact.4821@tutorax.com", login);
    }

    [Fact]
    public void ResolveCredentialRecipients_without_teacher_email()
    {
        var recipients = TeacherLoginProvisioning.ResolveCredentialRecipients(
            null, "manager@groupe.com");
        Assert.Contains("manager@groupe.com", recipients);
        Assert.Contains(TeacherLoginProvisioning.DefaultPlatformOpsEmail, recipients);
        Assert.Equal(2, recipients.Count);
    }

    [Fact]
    public void ResolveCredentialRecipients_with_teacher_email()
    {
        var recipients = TeacherLoginProvisioning.ResolveCredentialRecipients(
            "enseignant@perso.com", "manager@groupe.com");
        Assert.Equal(3, recipients.Count);
        Assert.Contains("enseignant@perso.com", recipients);
    }
}
