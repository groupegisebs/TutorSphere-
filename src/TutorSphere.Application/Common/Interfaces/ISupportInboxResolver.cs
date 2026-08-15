namespace TutorSphere.Application.Common.Interfaces;

/// <summary>Utilisateur plateforme qui reçoit les dossiers d'assistance parent (sans exposer son courriel).</summary>
public interface ISupportInboxResolver
{
    Task<string?> ResolveUserIdAsync(CancellationToken ct = default);
}
