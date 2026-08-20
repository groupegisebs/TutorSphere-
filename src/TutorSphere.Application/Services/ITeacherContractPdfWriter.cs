using TutorSphere.Application.DTOs.Contracts;

namespace TutorSphere.Application.Services;

public interface ITeacherContractPdfWriter
{
    Task<(string RelativePath, string Sha256)> WriteSignedPdfAsync(
        TeacherContractPdfModel model,
        CancellationToken ct = default);

    Task<byte[]?> ReadPdfAsync(string relativePath, CancellationToken ct = default);
}

public sealed class TeacherContractPdfModel
{
    public required string ContractNumber { get; init; }
    public required string Version { get; init; }
    public required string Language { get; init; }
    public required string GroupName { get; init; }
    public required string TeacherName { get; init; }
    public required DateTime SignedAtUtc { get; init; }
    public required string VerificationCode { get; init; }
    public required string VerificationUrl { get; init; }
    public required string DocumentHashPlaceholder { get; init; }
    public required IReadOnlyList<(string Title, string Body)> Sections { get; init; }
    public string? SignaturePngBase64 { get; init; }
    public string? GroupSignatoryName { get; init; }
    public string? GroupSignatoryRole { get; init; }
}
