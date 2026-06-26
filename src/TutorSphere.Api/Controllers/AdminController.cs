using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "SuperAdmin,PlatformAdmin")]
public class AdminController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    /// <summary>Returns users belonging to a given role.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string role = "Tutor")
    {
        if (!UserRoles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Rôle inconnu." });

        var users = await _userManager.GetUsersInRoleAsync(role);
        var result = users
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email ?? string.Empty,
                u.FullName,
                role,
                u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow))
            .OrderBy(u => u.FullName)
            .ToList();

        return Ok(result);
    }

    /// <summary>Unlocks a user account.</summary>
    [HttpPost("users/{userId}/activate")]
    public async Task<IActionResult> ActivateUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound(new { error = "Utilisateur introuvable." });

        user.LockoutEnd = null;
        user.LockoutEnabled = false;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });

        return Ok(new { message = "Compte activé." });
    }

    /// <summary>Locks a user account indefinitely.</summary>
    [HttpPost("users/{userId}/deactivate")]
    public async Task<IActionResult> DeactivateUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound(new { error = "Utilisateur introuvable." });

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });

        return Ok(new { message = "Compte désactivé." });
    }

    /// <summary>Returns aggregate counts used by the admin dashboard.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var tutors   = (await _userManager.GetUsersInRoleAsync(UserRoles.Tutor)).ToList();
        var parents  = (await _userManager.GetUsersInRoleAsync(UserRoles.Parent)).ToList();
        var students = (await _userManager.GetUsersInRoleAsync(UserRoles.Student)).ToList();

        var all      = tutors.Concat(parents).Concat(students).ToList();
        var active   = all.Count(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow);
        var inactive = all.Count - active;

        return Ok(new AdminStatsDto(tutors.Count, parents.Count, students.Count, active, inactive));
    }
}

public sealed record AdminUserDto(string Id, string Email, string FullName, string Role, bool IsActive);
public sealed record AdminStatsDto(int TotalTutors, int TotalParents, int TotalStudents, int ActiveUsers, int InactiveUsers);
