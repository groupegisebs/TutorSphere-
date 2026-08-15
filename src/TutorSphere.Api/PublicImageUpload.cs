using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace TutorSphere.Api;

public static class PublicImageUpload
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };

    public static string? ResolveExtension(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!string.IsNullOrWhiteSpace(extension) && AllowedExtensions.Contains(extension))
            return extension.ToLowerInvariant();

        return file.ContentType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/webp" => ".webp",
            _ => null
        };
    }

    public static ActionResultError? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return new("Fichier requis.");
        if (file.Length > MaxBytes)
            return new("Fichier trop volumineux (max. 5 Mo).");
        if (string.IsNullOrWhiteSpace(ResolveExtension(file)))
            return new("Format non supporté. Utilisez PNG, JPG ou WebP.");
        return null;
    }

    public static async Task<string> SaveAsync(
        IWebHostEnvironment env,
        IFormFile file,
        string fileNameWithoutExtension,
        CancellationToken ct)
    {
        var extension = ResolveExtension(file) ?? ".png";
        var safe = $"{fileNameWithoutExtension}{extension}";
        var root = UploadsPaths.GetRoot(env);
        var path = Path.Combine(root, safe);
        await using var stream = File.Create(path);
        await file.CopyToAsync(stream, ct);
        return $"/uploads/{safe}";
    }

    public sealed record ActionResultError(string Message);
}
