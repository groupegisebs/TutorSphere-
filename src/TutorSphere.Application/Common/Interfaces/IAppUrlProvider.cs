namespace TutorSphere.Application.Common.Interfaces;

public interface IAppUrlProvider
{
    /// <summary>URL publique du site Blazor (liens e-mail, redirections).</summary>
    string WebBaseUrl { get; }

    /// <summary>URL publique de l'API (jamais qisebs/qiscbs — typos corrigées).</summary>
    string ApiPublicBaseUrl { get; }

    /// <summary>Lien de confirmation e-mail (passe par le site Web, SSL NPM).</summary>
    string BuildEmailConfirmUrl(string userId, string token);
}
