using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TutorSphere.Application.DTOs.Auth;
using TutorSphere.Infrastructure.Identity;
using TutorSphere.Infrastructure.Persistence;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _db;

    public AuthController(
        IAuthService authService,
        IConfiguration configuration,
        ApplicationDbContext db)
    {
        _authService = authService;
        _configuration = configuration;
        _db = db;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _authService.RegisterAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("register-school")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterSchoolResponse>> RegisterSchool(
        [FromBody] RegisterSchoolRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _authService.RegisterSchoolAsync(request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(
        [FromQuery] string userId,
        [FromQuery] string token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "Paramètres manquants." });

        try
        {
            await _authService.ConfirmEmailAsync(userId, token, ct);

            var webBase = (_configuration["WebBaseUrl"] ?? "").TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(webBase))
                return Redirect($"{webBase}/login?confirmed=true");

            return Content("""
                <!doctype html>
                <html lang="fr">
                <head><meta charset="utf-8"><title>Adresse confirmée — TutorSphere</title>
                <style>body{font-family:sans-serif;display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;background:#f8f7ff;}
                .box{text-align:center;padding:2rem;background:#fff;border-radius:12px;box-shadow:0 2px 12px rgba(0,0,0,.08);max-width:420px;}
                .icon{font-size:3rem;}h1{color:#5b21b6;}p{color:#555;}a{color:#7c3aed;font-weight:600;}</style>
                </head>
                <body><div class="box">
                <div class="icon">✅</div>
                <h1>Adresse confirmée !</h1>
                <p>Votre compte TutorSphere est maintenant activé.</p>
                <p><a href="/login">Connexion →</a></p>
                </div></body></html>
                """, "text/html");
        }
        catch (InvalidOperationException ex)
        {
            var msg = System.Net.WebUtility.HtmlEncode(ex.Message);
            var errorHtml = "<html lang='fr'><head><meta charset='utf-8'><title>Erreur - TutorSphere</title>"
                + "<style>body{font-family:sans-serif;display:flex;align-items:center;justify-content:center;min-height:100vh;background:#fff5f5}"
                + ".box{text-align:center;padding:2rem;background:#fff;border-radius:12px;max-width:420px}"
                + "h1{color:#dc2626}</style></head><body><div class='box'>"
                + "<h1>Lien invalide</h1><p>" + msg + "</p></div></body></html>";
            return Content(errorHtml, "text/html");
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _authService.LoginAsync(request, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    /// <summary>Connexion élève : e-mail du parent + code d'accès généré pour l'enfant.</summary>
    [HttpPost("login-child")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> LoginChild([FromBody] ChildLoginRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _authService.LoginChildAsync(request, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lightweight auth/DB diagnostics (no secrets). Use after deploy to verify seed accounts and JWT config.
    /// </summary>
    [HttpGet("diagnostics")]
    [AllowAnonymous]
    public async Task<IActionResult> Diagnostics(CancellationToken ct)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "";
        var dbOk = false;
        string? dbError = null;
        int userCount = 0;

        try
        {
            dbOk = await _db.Database.CanConnectAsync(ct);
            if (dbOk)
                userCount = await _db.Users.CountAsync(ct);
        }
        catch (Exception ex)
        {
            dbError = ex.Message;
        }

        return Ok(new
        {
            database = new { connected = dbOk, error = dbError, userCount },
            jwt = new
            {
                configured = jwtKey.Length >= 32,
                issuer = _configuration["Jwt:Issuer"],
                audience = _configuration["Jwt:Audience"]
            },
            seed = new
            {
                includeDemoData = _configuration.GetValue("Seed:IncludeDemoData", false),
                removeLegacyBootstrapUsers = _configuration.GetValue("Seed:RemoveLegacyBootstrapUsers", true),
                bootstrapAdminEnabled = _configuration.GetValue("Seed:BootstrapAdmin:Enabled", false)
            }
        });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _authService.ForgotPasswordAsync(request.Email, ct);
        return Ok(new { message = "Si cette adresse e-mail est associée à un compte, un lien de réinitialisation a été envoyé." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        try
        {
            await _authService.ResetPasswordAsync(request.UserId, request.Token, request.NewPassword, ct);
            return Ok(new { message = "Mot de passe réinitialisé avec succès." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
