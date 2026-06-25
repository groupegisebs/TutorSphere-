using TutorSphere.Domain.Enums;

namespace TutorSphere.UnitTests;

public class UserRolesTests
{
    [Theory]
    [InlineData("Tutor")]
    [InlineData("Parent")]
    [InlineData("Student")]
    public void All_roles_include_expected_values(string role)
    {
        Assert.Contains(role, UserRoles.All);
    }

    [Fact]
    public void All_roles_has_six_entries()
    {
        Assert.Equal(6, UserRoles.All.Length);
    }
}
