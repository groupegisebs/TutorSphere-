using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Infrastructure.Services;

public class ExpertIdentityActions(
    UserManager<ApplicationUser> userManager,
    IEmailService email,
    IAppUrlProvider urls,
    ILogger<ExpertIdentityActions> logger) : IExpertIdentityActions
{
    public async Task<string?> FindUserIdByEmailAsync(string email, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        return user?.Id;
    }

    public async Task EnsureExpertRoleAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");
        if (!await userManager.IsInRoleAsync(user, Domain.Enums.UserRoles.Expert))
            await userManager.AddToRoleAsync(user, Domain.Enums.UserRoles.Expert);
    }

    public async Task<string> EnsureCandidateUserAsync(
        string email,
        string firstName,
        string lastName,
        string? password,
        CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(normalized);
        if (user is not null)
        {
            if (!string.IsNullOrWhiteSpace(firstName)) user.FirstName = firstName.Trim();
            if (!string.IsNullOrWhiteSpace(lastName)) user.LastName = lastName.Trim();
            await userManager.UpdateAsync(user);
            return user.Id;
        }

        var pwd = string.IsNullOrWhiteSpace(password)
            ? GenerateTemporaryPassword()
            : password;
        if (pwd.Length < 8 || !pwd.Any(char.IsDigit) || pwd.All(char.IsLetterOrDigit))
            throw new InvalidOperationException(
                "Mot de passe trop faible (8 caractères, un chiffre et un caractère spécial).");

        user = new ApplicationUser
        {
            UserName = normalized,
            Email = normalized,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            EmailConfirmed = true,
            MustChangePassword = string.IsNullOrWhiteSpace(password)
        };
        var create = await userManager.CreateAsync(user, pwd);
        if (!create.Succeeded)
            throw new InvalidOperationException(string.Join("; ", create.Errors.Select(e => e.Description)));
        return user.Id;
    }

    public async Task NotifyExpertAdmittedAsync(string userId, string groupName, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user?.Email is null) return;

        var loginUrl = $"{urls.WebBaseUrl.TrimEnd('/')}/login/expert";
        try
        {
            // Prefer reset-password link when MustChangePassword; otherwise notify without password.
            if (user.MustChangePassword)
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var resetUrl =
                    $"{urls.WebBaseUrl.TrimEnd('/')}/reset-password?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";
                await email.SendResetPasswordAsync(user.Email, user.FirstName ?? "expert", resetUrl, ct);
            }
            else
            {
                await email.SendExpertAddedToGroupAsync(
                    user.Email, user.FirstName ?? "expert", loginUrl, groupName, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Échec notification admission expert {UserId}", userId);
        }
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$";
        var bytes = RandomNumberGenerator.GetBytes(14);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray()) + "1!";
    }
}
