using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.Contracts;

public record ContractClientContext(string? Ip, string? UserAgent);

public record TeacherContractVariableDto(string Key, string Label, string Value);

public record TeacherContractTemplateDto(
    string Version,
    IReadOnlyList<TeacherContractVariableDto> Variables);

public record TeacherContractTeacherOptionDto(
    Guid TenantId,
    string Name,
    string? Email,
    string? GroupName);

public record SendTeacherContractRequest(
    Guid TenantId,
    string? Language = null,
    Dictionary<string, string>? Variables = null);

public record TeacherContractListItemDto(
    Guid Id,
    string ContractNumber,
    string Version,
    string Language,
    TeacherContractStatus Status,
    Guid TenantId,
    string TeacherName,
    string? TeacherEmail,
    string GroupName,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? SignedAt,
    DateTime? TokenExpiresAt,
    bool CanDownload);

public record TeacherContractSectionDto(
    string Key,
    string Title,
    string Body,
    bool Opened,
    bool? Accepted);

public record TeacherContractDetailDto(
    Guid Id,
    string ContractNumber,
    string Version,
    string Language,
    TeacherContractStatus Status,
    string GroupName,
    string TeacherName,
    DateTime? TokenExpiresAt,
    DateTime? SignedAt,
    string? PdfUrl,
    string? DocumentHash,
    string? VerificationCode,
    string? VerificationUrl,
    string SignUrl,
    IReadOnlyList<TeacherContractSectionDto> Sections,
    IReadOnlyList<TeacherContractAuditDto> Audit,
    bool AllSectionsAccepted,
    string? RefusalComment);

public record TeacherContractSignViewDto(
    Guid Id,
    string ContractNumber,
    string Version,
    string Language,
    TeacherContractStatus Status,
    string GroupName,
    string TeacherName,
    DateTime? TokenExpiresAt,
    IReadOnlyList<TeacherContractSectionDto> Sections,
    bool AllSectionsAccepted,
    string IdentityFullNameHint,
    string? GroupLogoUrl = null);

public record DecideContractSectionRequest(bool Accept, string? Comment = null);

public record RefuseContractRequest(string SectionKey, string? Comment = null);

public record CompleteContractSignatureRequest(
    string TypedFullName,
    string SignaturePngBase64,
    bool FinalConsent);

public record TeacherContractAuditDto(
    DateTime At,
    TeacherContractAuditAction Action,
    string Summary,
    string? ActorName);

public record TeacherContractVerifyDto(
    string ContractNumber,
    string Version,
    string Language,
    TeacherContractStatus Status,
    DateTime? SignedAt,
    string? DocumentHash,
    string? VerificationCode,
    bool Authentic);
