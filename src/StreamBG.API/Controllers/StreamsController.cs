using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StreamBG.API.Services;

namespace StreamBG.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StreamsController : ControllerBase
{
    private readonly IStreamService _streams;

    public StreamsController(IStreamService streams) => _streams = streams;

    /// <summary>Всички активни стриймове (за главната страница)</summary>
    [HttpGet]
    public async Task<IActionResult> GetLive([FromQuery] string? category, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _streams.GetLiveStreamsAsync(category, page, pageSize);
        return Ok(result);
    }

    /// <summary>Информация за конкретен стрийм по username</summary>
    [HttpGet("{username}")]
    public async Task<IActionResult> GetByUsername(string username)
    {
        var stream = await _streams.GetStreamByUsernameAsync(username);
        return stream is not null ? Ok(stream) : NotFound();
    }

    /// <summary>Създаване/обновяване на собствения стрийм</summary>
    [HttpPut("settings")]
    [Authorize]
    public async Task<IActionResult> UpdateSettings([FromBody] StreamSettingsRequest req)
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await _streams.UpdateStreamSettingsAsync(userId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Вземане на собствения stream key</summary>
    [HttpGet("my/key")]
    [Authorize]
    public async Task<IActionResult> GetMyStreamKey()
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;
        var key = await _streams.GetStreamKeyAsync(userId);
        return Ok(new { streamKey = key });
    }

    /// <summary>Генериране на нов stream key</summary>
    [HttpPost("my/regenerate-key")]
    [Authorize]
    public async Task<IActionResult> RegenerateKey()
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;
        var key = await _streams.RegenerateStreamKeyAsync(userId);
        return Ok(new { streamKey = key });
    }

    /// <summary>Настройки за социални мрежи</summary>
    [HttpPut("my/social")]
    [Authorize]
    public async Task<IActionResult> UpdateSocialSettings([FromBody] SocialRelayRequest req)
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await _streams.UpdateSocialRelayAsync(userId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>История на стриймовете</summary>
    [HttpGet("my/history")]
    [Authorize]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 1)
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;
        var history = await _streams.GetStreamHistoryAsync(userId, page);
        return Ok(history);
    }

    /// <summary>Категории</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var cats = await _streams.GetCategoriesAsync();
        return Ok(cats);
    }
}

public record StreamSettingsRequest(
    string Title,
    string? Description,
    string? Category,
    bool RelayToYouTube = false,
    bool RelayToFacebook = false,
    bool RelayToTikTok = false
);

public record SocialRelayRequest(
    string? YouTubeStreamKey,
    string? FacebookStreamKey,
    string? TikTokStreamKey,
    bool RelayToYouTube = false,
    bool RelayToFacebook = false,
    bool RelayToTikTok = false
);
