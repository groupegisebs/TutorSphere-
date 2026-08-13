using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.ExpertApproval;

public record CreateExpertMembershipInviteRequest(
    string Email,
    string FirstName,
    string LastName,
    string? Phone = null,
    string? Specialty = null,
    string? IntendedRole = null,
    string? Presentation = null,
    string? Justification = null,
    string? PersonalMessage = null);

public record SubmitExpertMembershipCandidacyRequest(
    string Token,
    string? Password = null,
    string? FirstName = null,
    string? LastName = null,
    string? Phone = null,
    string? Specialty = null,
    string? Presentation = null,
    bool AcceptedConduct = false,
    bool AcceptedPrivacy = false);

public record CastExpertMembershipVoteRequest(
    ExpertMembershipVoteChoice Choice,
    string? Comment = null);

public record AdminExpertMembershipActionRequest(
    string? Notes = null,
    int? ExtendInviteDays = null,
    int? ExtendVoteDays = null);

public record ExpertMembershipInvitePublicDto(
    Guid Id,
    string GroupName,
    string? GroupCountryCode,
    string InviterName,
    string Email,
    string FirstName,
    string LastName,
    ExpertMembershipInviteStatus Status,
    DateTime InviteExpiresAtUtc,
    bool RequiresAccountCreation);

public record ExpertMembershipVoteDto(
    string VoterUserId,
    string? VoterName,
    ExpertMembershipVoteChoice Choice,
    string? Comment,
    DateTime VotedAtUtc);

public record ExpertMembershipInviteDto(
    Guid Id,
    Guid ExpertGroupId,
    string GroupName,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string? Specialty,
    string? IntendedRole,
    string? Presentation,
    string? Justification,
    string InvitedByUserId,
    string? InvitedByName,
    ExpertMembershipInviteStatus Status,
    DateTime SentAtUtc,
    DateTime InviteExpiresAtUtc,
    DateTime? VoteOpenedAtUtc,
    DateTime? VoteExpiresAtUtc,
    int EligibleVoterCount,
    int RequiredApprovalCount,
    int ApprovalCount,
    int RejectCount,
    int AbstainCount,
    int? MyVote,
    IReadOnlyList<ExpertMembershipVoteDto> Votes,
    string? CandidateUserId,
    DateTime? DecisionAtUtc,
    string? AdminNotes);

public record ExpertGroupMemberListItemDto(
    Guid Id,
    Guid ExpertGroupId,
    string UserId,
    string Email,
    string FullName,
    ExpertMembershipStatus Status,
    ExpertAdmissionMethod AdmissionMethod,
    string? Specialty,
    DateTime? AdmittedAtUtc,
    ExpertGroupMemberRole MemberRole = ExpertGroupMemberRole.Expert);
