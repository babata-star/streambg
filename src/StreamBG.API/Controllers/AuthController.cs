using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StreamBG.API.Services;

namespace StreamBG.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Регистрация на нов потребител</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var result = await _auth.RegisterAsync(req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Влизане в системата</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await _auth.LoginAsync(req.Email, req.Password);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>Обновяване на токена</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var result = await _auth.RefreshTokenAsync(req.RefreshToken);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>Излизане</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? req)
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;
        await _auth.LogoutAsync(userId, req?.RefreshToken);
        return Ok(new { message = "Успешно излязохте от системата" });
    }

    /// <summary>Текущ потребител</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;
        var user = await _auth.GetCurrentUserAsync(userId);
        return user is not null ? Ok(user) : NotFound();
    }
}

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string? Bio = null
);

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string? RefreshToken = null);
