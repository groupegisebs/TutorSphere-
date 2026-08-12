using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace TutorSphere.Api;

/// <summary>
/// Résout le dossier des fichiers uploadés.
/// En production Docker le volume est monté sur <c>/app/uploads</c>
/// (= <see cref="IWebHostEnvironment.ContentRootPath"/>/uploads).
/// Ne jamais utiliser <c>WebRootPath/uploads</c> : ce chemin n'est pas persisté
/// et diverge du volume Docker, ce qui produit des logos PNG « cassés ».
/// </summary>
public static class UploadsPaths
{
    public const string FolderName = "uploads";
    public const string RequestPath = "/uploads";

    public static string GetRoot(IWebHostEnvironment env)
    {
        var primary = Path.Combine(env.ContentRootPath, FolderName);
        Directory.CreateDirectory(primary);

        // Legacy fallback: older builds wrote under wwwroot/uploads.
        var legacy = GetLegacyRoot(env);
        if (legacy is not null && Directory.Exists(legacy))
            TryMigrateLegacyFiles(legacy, primary);

        return primary;
    }

    /// <summary>Finds an uploaded file on the primary volume, then the legacy wwwroot path.</summary>
    public static string? FindExistingFile(IWebHostEnvironment env, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;
        var safe = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(safe))
            return null;

        var primaryPath = Path.Combine(GetRoot(env), safe);
        if (File.Exists(primaryPath))
            return primaryPath;

        var legacy = GetLegacyRoot(env);
        if (legacy is null)
            return null;
        var legacyPath = Path.Combine(legacy, safe);
        return File.Exists(legacyPath) ? legacyPath : null;
    }

    public static IFileProvider CreateFileProvider(IWebHostEnvironment env)
    {
        var primary = GetRoot(env);
        var providers = new List<IFileProvider> { new PhysicalFileProvider(primary) };

        var legacy = GetLegacyRoot(env);
        if (legacy is not null && Directory.Exists(legacy))
            providers.Add(new PhysicalFileProvider(legacy));

        return providers.Count == 1
            ? providers[0]
            : new CompositeFileProvider(providers);
    }

    private static string? GetLegacyRoot(IWebHostEnvironment env)
    {
        if (string.IsNullOrWhiteSpace(env.WebRootPath))
            return null;
        var legacy = Path.Combine(env.WebRootPath, FolderName);
        if (string.Equals(Path.GetFullPath(legacy), Path.GetFullPath(Path.Combine(env.ContentRootPath, FolderName)), StringComparison.OrdinalIgnoreCase))
            return null;
        return legacy;
    }

    private static void TryMigrateLegacyFiles(string legacyRoot, string primaryRoot)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(legacyRoot))
            {
                var dest = Path.Combine(primaryRoot, Path.GetFileName(file));
                if (File.Exists(dest))
                    continue;
                File.Copy(file, dest);
            }
        }
        catch
        {
            // Best-effort: serving still works via CompositeFileProvider.
        }
    }
}
