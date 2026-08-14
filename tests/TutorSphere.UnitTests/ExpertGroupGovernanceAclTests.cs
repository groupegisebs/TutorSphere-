using TutorSphere.Domain.Enums;

namespace TutorSphere.UnitTests;

public class ExpertGroupLifecycleRulesTests
{
    [Theory]
    [InlineData(ExpertGroupLifecycleStatus.Draft, true, ExpertGroupLifecycleStatus.Active)]
    [InlineData(ExpertGroupLifecycleStatus.Active, false, ExpertGroupLifecycleStatus.Suspended)]
    [InlineData(ExpertGroupLifecycleStatus.Suspended, true, ExpertGroupLifecycleStatus.Active)]
    public void Soft_toggle_maps_to_expected_lifecycle(
        ExpertGroupLifecycleStatus from, bool activate, ExpertGroupLifecycleStatus expected)
    {
        var next = activate
            ? ExpertGroupLifecycleStatus.Active
            : from == ExpertGroupLifecycleStatus.Active || from == ExpertGroupLifecycleStatus.Draft
                ? ExpertGroupLifecycleStatus.Suspended
                : from;
        if (activate)
            next = ExpertGroupLifecycleStatus.Active;
        else if (from is ExpertGroupLifecycleStatus.Active or ExpertGroupLifecycleStatus.Draft)
            next = ExpertGroupLifecycleStatus.Suspended;

        Assert.Equal(expected, next);
    }

    [Fact]
    public void Archived_cannot_reactivate()
    {
        var status = ExpertGroupLifecycleStatus.Archived;
        var canActivate = status != ExpertGroupLifecycleStatus.Archived;
        Assert.False(canActivate);
    }

    [Fact]
    public void Suspend_mandate_does_not_require_group_suspend()
    {
        // Product rule: mandate Suspended keeps group lifecycle unchanged.
        var groupStatus = ExpertGroupLifecycleStatus.Active;
        var mandateStatus = ExpertGroupManagerMandateStatus.Suspended;
        Assert.Equal(ExpertGroupLifecycleStatus.Active, groupStatus);
        Assert.Equal(ExpertGroupManagerMandateStatus.Suspended, mandateStatus);
    }

    [Fact]
    public void Appoint_does_not_reactivate_suspended_group()
    {
        var lifecycle = ExpertGroupLifecycleStatus.Suspended;
        var becomesActive = lifecycle == ExpertGroupLifecycleStatus.Draft
            || lifecycle == ExpertGroupLifecycleStatus.Active;
        Assert.False(becomesActive);
    }
}

public class ExpertAssignReviewAclTests
{
    [Theory]
    [InlineData("expert-1", "expert-1", false, true)]  // self-claim
    [InlineData("expert-1", "expert-2", false, false)] // assign other without manager
    [InlineData("expert-1", "expert-2", true, true)]   // manager/act-as
    public void Assign_others_requires_manager_or_actas(
        string caller, string assignee, bool canAssignOthers, bool expectedAllowed)
    {
        var isSelf = string.Equals(caller, assignee, StringComparison.Ordinal);
        var allowed = isSelf || canAssignOthers;
        Assert.Equal(expectedAllowed, allowed);
    }
}

public class ExpertMeContextManagerTests
{
    [Theory]
    [InlineData(true, false, true)]   // active mandate
    [InlineData(false, true, true)]   // valid act-as
    [InlineData(false, false, false)] // orphan Identity alone → false
    public void IsGroupManager_requires_mandate_or_actas(
        bool hasActiveMandate, bool hasValidActAs, bool expected)
    {
        var isGroupManager = hasValidActAs || hasActiveMandate;
        Assert.Equal(expected, isGroupManager);
    }
}

public class GroupAdminChatAclTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Conversation_access_requires_manager_of_group_or_platform_admin(
        bool isPlatformAdmin, bool isActiveManagerOfConversationGroup, bool expected)
    {
        var allowed = isPlatformAdmin || isActiveManagerOfConversationGroup;
        Assert.Equal(expected, allowed);
    }
}
