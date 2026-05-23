using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StreamBG.API.Services;
using StreamBG.Infrastructure.Services;

namespace StreamBG.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private readonly IUserService _users;
    private readonly IStreamService _streams;
    private readonly ICdnService _cdn;

    public AdminController(IUserService users, IStreamService streams, ICdnService cdn)
    {
        _users = users;
        _streams = streams;
        _cdn = cdn;
    }

    // ── Dashboard Statistics ──────────────────────────────────────────────────

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _streams.GetPlatformStatsAsync();
        return Ok(stats);
    }

    // ── User Management ───────────────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var users = await _users.GetAllUsersAsync(search, page, pageSize);
        return Ok(users);
    }

    [HttpPost("users/{userId}/ban")]
    public async Task<IActionResult> BanUser(string userId, [FromBody] BanRequest req)
    {
        await _users.BanUserAsync(userId, req.Reason);
        return Ok(new { message = $"Потребителят е блокиран: {req.Reason}" });
    }

    [HttpPost("users/{userId}/unban")]
    public async Task<IActionResult> UnbanUser(string userId)
    {
        await _users.UnbanUserAsync(userId);
        return Ok(new { message = "Потребителят е разблокиран" });
    }

    [HttpPost("users/{userId}/make-streamer")]
    public async Task<IActionResult> MakeStreamer(string userId)
    {
        await _users.SetStreamerRoleAsync(userId, true);
        return Ok(new { message = "Потребителят вече може да стриймва" });
    }

    // ── Stream Management ─────────────────────────────────────────────────────

    [HttpGet("streams/live")]
    public async Task<IActionResult> GetAllLiveStreams()
    {
        var streams = await _streams.GetAllLiveStreamsAdminAsync();
        return Ok(streams);
    }

    [HttpPost("streams/{streamId}/terminate")]
    public async Task<IActionResult> TerminateStream(int streamId)
    {
        await _streams.TerminateStreamAsync(streamId);
        return Ok(new { message = "Стриймът е прекратен" });
    }

    // ── Chat Moderation ───────────────────────────────────────────────────────

    [HttpDelete("chat/{messageId}")]
    public async Task<IActionResult> DeleteChatMessage(int messageId)
    {
        await _streams.DeleteChatMessageAsync(messageId);
        return Ok(new { message = "Съобщението е изтрито" });
    }

    // ── CDN Management ────────────────────────────────────────────────────────

    /// <summary>
    /// Purge specific CDN paths. Accepts glob patterns, e.g. /hls/key/* or /vod/userId/123/*.
    /// </summary>
    [HttpPost("cdn/invalidate")]
    public async Task<IActionResult> InvalidateCdn([FromBody] CdnInvalidateRequest req)
    {
        if (!_cdn.IsEnabled)
            return Ok(new { message = "CDN е изключен — нищо не е изчистено" });

        if (req.Patterns.Length == 0)
            return BadRequest(new { error = "Нужен е поне един path pattern" });

        await _cdn.InvalidateAsync(req.Patterns);
        return Ok(new { message = $"Изпратена заявка за {req.Patterns.Length} path(s)" });
    }

    [HttpPost("cdn/invalidate/stream/{streamKey}")]
    public async Task<IActionResult> InvalidateStream(string streamKey)
    {
        await _cdn.InvalidateAsync($"/hls/{streamKey}/*");
        return Ok(new { message = $"CDN cache изчистен за стрийм {streamKey}" });
    }

    [HttpPost("cdn/invalidate/vod/{userId}/{vodId:int}")]
    public async Task<IActionResult> InvalidateVod(string userId, int vodId)
    {
        await _cdn.InvalidateAsync($"/vod/{userId}/{vodId}/*");
        return Ok(new { message = $"CDN cache изчистен за VOD {vodId}" });
    }
}

public record BanRequest(string Reason);
public record CdnInvalidateRequest(string[] Patterns);
