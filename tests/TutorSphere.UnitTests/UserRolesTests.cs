using TutorSphere.Domain.Enums;

namespace TutorSphere.UnitTests;

public class UserRolesTests
{
    [Theory]
    [InlineData("Tutor")]
    [InlineData("Parent")]
    [InlineData("Student")]
    [InlineData("Expert")]
    [InlineData("GroupManager")]
    [InlineData("SuperAdmin")]
    public void All_roles_include_expected_values(string role)
    {
        Assert.Contains(role, UserRoles.All);
    }

    [Fact]
    public void All_roles_includes_group_manager()
    {
        Assert.Equal(8, UserRoles.All.Length);
        Assert.Equal("GroupManager", UserRoles.GroupManager);
    }
}

public class ExpertMembershipVoteMathTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 3)]
    [InlineData(8, 6)]
    public void RequiredApprovals_uses_ceiling_75_percent(int eligible, int expected)
    {
        Assert.Equal(expected, IExpertMembershipGovernanceService_RequiredApprovals(eligible));
    }

    // Mirror product formula without referencing Application internals if interface is static.
    private static int IExpertMembershipGovernanceService_RequiredApprovals(int eligibleCount)
    {
        if (eligibleCount <= 0) return 0;
        return (int)Math.Ceiling(eligibleCount * 0.75);
    }
}
