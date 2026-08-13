using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IExpertMembershipGovernanceService
{
    Task<ExpertMembershipInviteDto> CreateInviteAsync(
        string initiatorUserId,
        CreateExpertMembershipInviteRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<ExpertMembershipInviteDto>> ListForExpertAsync(
        string expertUserId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ExpertMembershipInviteDto>> ListForAdminAsync(
        Guid? groupId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ExpertGroupMemberListItemDto>> ListActiveMembersAsync(
        string expertUserId,
        CancellationToken ct = default);

    Task<ExpertMembershipInvitePublicDto> GetPublicInviteAsync(string token, CancellationToken ct = default);

    Task<ExpertMembershipInviteDto> SubmitCandidacyAsync(
        SubmitExpertMembershipCandidacyRequest request,
        CancellationToken ct = default);

    Task DeclineInviteAsync(string token, CancellationToken ct = default);

    Task<ExpertMembershipInviteDto> CastVoteAsync(
        string voterUserId,
        Guid inviteId,
        CastExpertMembershipVoteRequest request,
        CancellationToken ct = default);

    Task<ExpertMembershipInviteDto> AdminForceApproveAsync(
        string adminUserId,
        Guid inviteId,
        AdminExpertMembershipActionRequest? request,
        CancellationToken ct = default);

    Task<ExpertMembershipInviteDto> AdminForceRejectAsync(
        string adminUserId,
        Guid inviteId,
        AdminExpertMembershipActionRequest? request,
        CancellationToken ct = default);

    Task<ExpertMembershipInviteDto> AdminCancelAsync(
        string adminUserId,
        Guid inviteId,
        AdminExpertMembershipActionRequest? request,
        CancellationToken ct = default);

    Task<ExpertMembershipInviteDto> AdminExtendAsync(
        string adminUserId,
        Guid inviteId,
        AdminExpertMembershipActionRequest request,
        CancellationToken ct = default);

    Task<ExpertMembershipInviteDto> AdminValidateSmallGroupAsync(
        string adminUserId,
        Guid inviteId,
        AdminExpertMembershipActionRequest? request,
        CancellationToken ct = default);

    static int RequiredApprovals(int eligibleCount)
    {
        if (eligibleCount <= 0) return 0;
        return (int)Math.Ceiling(eligibleCount * 0.75);
    }
}
