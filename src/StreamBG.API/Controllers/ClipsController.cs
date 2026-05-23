using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StreamBG.API.Services;

namespace StreamBG.API.Controllers;

[ApiController]
[Route("api/clips")]
public class ClipsController : ControllerBase
{
    private readonly IClipService _clips;

    public ClipsController(IClipService clips) => _clips = clips;

    // ── Create clip ────────────────────────────────────────────────────────────

    /// <summary>
    /// Create a 30-second clip from a live stream or a VOD.
    /// Returns immediately with the clip ID; the video is ready once status = Ready.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateClipRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var (clipId, error) = await _clips.CreateClipAsync(userId, req);

        if (error is not null)
            return BadRequest(new { error });

        return Accepted(new { clipId, message = "Клипът се обработва — проверете статуса след малко." });
    }

    // ── Get single clip ───────────────────────────────────────────────────────

    [HttpGet("{clipId:int}")]
    public async Task<IActionResult> Get(int clipId)
    {
        var dto = await _clips.GetClipAsync(clipId);
        return dto is null ? NotFound() : Ok(dto);
    }

    // ── Browse clips ──────────────────────────────────────────────────────────

    /// <summary>
    /// Browse public ready clips.
    /// ?streamerId=...  filter by streamer user ID
    /// ?sort=newest|popular
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? streamerId,
        [FromQuery] string? category,
        [FromQuery] string  sort     = "newest",
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 24)
    {
        pageSize = Math.Clamp(pageSize, 1, 48);
        var result = await _clips.GetClipsAsync(streamerId, category, sort, page, pageSize);
        return Ok(result);
    }

    // ── Streamer's clips ──────────────────────────────────────────────────────

    [HttpGet("streamer/{streamerId}")]
    public async Task<IActionResult> GetByStreamer(
        string streamerId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 24)
    {
        pageSize = Math.Clamp(pageSize, 1, 48);
        var clips = await _clips.GetStreamerClipsAsync(streamerId, page, pageSize);
        return Ok(clips);
    }

    // ── Record view ───────────────────────────────────────────────────────────

    [HttpPost("{clipId:int}/view")]
    public async Task<IActionResult> RecordView(int clipId)
    {
        await _clips.IncrementViewAsync(clipId);
        return NoContent();
    }

    // ── Delete clip ───────────────────────────────────────────────────────────

    [HttpDelete("{clipId:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int clipId)
    {
        var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var isAdmin = User.HasClaim("role", "admin");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var ok = await _clips.DeleteClipAsync(userId, isAdmin, clipId);
        return ok ? NoContent() : NotFound();
    }
}

// Pulled in from System.IdentityModel.Tokens.Jwt so we don't need an extra using
file static class JwtRegisteredClaimNames
{
    internal const string Sub = "sub";
}
