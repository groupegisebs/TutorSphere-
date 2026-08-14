using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.ExpertGroupGovernance;

public record ExpertGroupManagerDto(
    Guid MandateId,
    Guid MembershipId,
    string UserId,
    string FullName,
    string Email,
    string? Phone,
    string? FunctionTitle,
    ExpertGroupManagerMandateStatus Status,
    DateTime MandateStartsAtUtc,
    DateTime? MandateEndsAtUtc,
    bool IsTemporary);

public record ExpertGroupManagerMandateHistoryDto(
    Guid MandateId,
    string UserId,
    ExpertGroupManagerMandateStatus Status,
    string? FunctionTitle,
    string? Phone,
    DateTime MandateStartsAtUtc,
    DateTime? MandateEndsAtUtc,
    bool IsTemporary,
    string AppointedByAdminId,
    string? EndedByAdminId,
    string? EndReason);

public record AppointGroupManagerRequest(
    string? ExistingUserId = null,
    string? Email = null,
    string? FirstName = null,
    string? LastName = null,
    string? Phone = null,
    string? FunctionTitle = null,
    DateTime? MandateStartsAtUtc = null,
    bool IsTemporary = false,
    bool CreateAccountIfMissing = true);

public record TransferGroupManagerRequest(
    string NewManagerUserId,
    string? Phone = null,
    string? FunctionTitle = null,
    DateTime? MandateStartsAtUtc = null,
    bool IsTemporary = false,
    string? EndReason = null);

public record SuspendGroupManagerRequest(string? Reason = null);

public record GroupOfferListItemDto(
    Guid Id,
    Guid ExpertGroupId,
    string Name,
    string? Code,
    GroupOfferStatus Status,
    GroupOfferPricingModel PricingModel,
    string Currency,
    decimal? RecommendedPrice,
    DateTime CreatedAt,
    DateTime? PublishedAtUtc,
    string? ShortDescription = null,
    bool IsInternational = false,
    string? MarketCountryCode = null,
    int AssignedTeacherCount = 0);

public record CreateGroupOfferRequest(
    string Name,
    string? Code = null,
    string? ShortDescription = null,
    Guid? DisciplineId = null,
    GroupOfferPricingModel PricingModel = GroupOfferPricingModel.Fixed,
    string? Currency = null,
    decimal? FixedPrice = null,
    decimal? MinimumPrice = null,
    decimal? RecommendedPrice = null,
    decimal? MaximumPrice = null,
    bool IsInternational = false,
    string? MarketCountryCode = null);

public record UpdateGroupOfferRequest(
    string Name,
    string? Code = null,
    string? ShortDescription = null,
    Guid? DisciplineId = null,
    GroupOfferPricingModel PricingModel = GroupOfferPricingModel.Fixed,
    string? Currency = null,
    decimal? FixedPrice = null,
    decimal? MinimumPrice = null,
    decimal? RecommendedPrice = null,
    decimal? MaximumPrice = null,
    bool IsInternational = false,
    string? MarketCountryCode = null);

public record GroupOffersCatalogDto(
    Guid ExpertGroupId,
    string GroupName,
    string? GroupCountryCode,
    string GroupCurrency,
    bool GroupIsInternational,
    IReadOnlyList<GroupOfferListItemDto> Offers);

public record GroupOfferAssignableTeacherDto(
    Guid TenantId,
    string SchoolName,
    string? OwnerName,
    string? City,
    string? Country);

public record GroupOfferTeacherAssignmentDto(
    Guid Id,
    Guid GroupOfferId,
    Guid TeacherTenantId,
    string TeacherName,
    GroupOfferTeacherAssignmentStatus AssignmentStatus,
    decimal? TeacherPrice,
    Guid? SubscriptionOfferingId,
    DateTime AssignedAtUtc);

public record AssignGroupOfferTeacherRequest(
    Guid TeacherTenantId,
    decimal? TeacherPrice = null,
    int? Capacity = null);

public record GroupAdminConversationDto(
    Guid Id,
    Guid ExpertGroupId,
    string GroupName,
    string Reference,
    string Subject,
    GroupAdminConversationCategory Category,
    GroupAdminConversationPriority Priority,
    GroupAdminConversationStatus Status,
    DateTime CreatedAt,
    int MessageCount,
    DateTime? LastMessageAt);

public record CreateGroupAdminConversationRequest(
    string Subject,
    GroupAdminConversationCategory Category,
    GroupAdminConversationPriority Priority,
    string Message,
    string? AttachmentReference = null);

public record PostGroupAdminMessageRequest(string Message, string? AttachmentReference = null);

public record GroupAdminMessageDto(
    Guid Id,
    string SenderUserId,
    string? SenderName,
    string Body,
    string? AttachmentReference,
    DateTime SentAtUtc,
    DateTime? ReadAtUtc,
    DateTime? EditedAtUtc);

public record TeacherInterestRequestDto(
    Guid Id,
    string FullName,
    string Email,
    string? CountryCode,
    string? City,
    string? Disciplines,
    string? Experience,
    string? Message,
    Guid? RoutedExpertGroupId,
    string? RoutedExpertGroupName,
    TeacherInterestRequestStatus Status,
    DateTime CreatedAt);

public record SubmitTeacherInterestRequest(
    string FullName,
    string Email,
    string? CountryCode = null,
    string? City = null,
    string? Disciplines = null,
    string? Experience = null,
    string? Message = null);
