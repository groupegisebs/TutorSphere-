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
    DateTime? PublishedAtUtc);

public record CreateGroupOfferRequest(
    string Name,
    string? Code = null,
    string? ShortDescription = null,
    Guid? DisciplineId = null,
    GroupOfferPricingModel PricingModel = GroupOfferPricingModel.Fixed,
    string Currency = "XAF",
    decimal? FixedPrice = null,
    decimal? MinimumPrice = null,
    decimal? RecommendedPrice = null,
    decimal? MaximumPrice = null);

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
