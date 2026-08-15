namespace TutorSphere.Application.Common.Interfaces;

public sealed record IssuedTeacherLogin(string Email, string TemporaryPassword);

/// <summary>Génère un mot de passe temporaire enseignant (après approbation du dossier).</summary>
public interface ITeacherLoginIssuer
{
    Task<IssuedTeacherLogin?> IssueTemporaryPasswordAsync(string userId, CancellationToken ct = default);
}
