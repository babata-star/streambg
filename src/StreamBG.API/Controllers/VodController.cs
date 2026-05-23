using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StreamBG.Core.Entities;
using StreamBG.Infrastructure.Data;
using StreamBG.Infrastructure.Services;

namespace StreamBG.API.Controllers;

[ApiController]
[Route("api/vod")]
public class VodController : ControllerBase
{
    private readonly StreamBGDbContext _db;
    private readonly ICdnService _cdn;

    public VodController(StreamBGDbContext db, ICdnService cdn)
    {
        _db = db;
        _cdn = cdn;
    }

    // ── Публични endpoints ────────────────────────────────────────────────────

    /// <summary>Всички публични VOD видеа (пагинирани)</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? username,
        [FromQuery] string? category,
        [FromQuery] string sort = "newest",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.VodVideos
            .Include(v => v.User)
            .Where(v => v.Status == VodStatus.Ready && v.IsPublic);

        if (!string.IsNullOrEmpty(username))
            query = query.Where(v => v.User.Username == username);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(v => v.Category == category);

        query = sort switch
        {
            "popular" => query.OrderByDescending(v => v.ViewCount),
            "oldest"  => query.OrderBy(v => v.CreatedAt),
            _         => query.OrderByDescending(v => v.CreatedAt)
        };

        var total = await query.CountAsync();
        var rawItems = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(v => new
            {
                v.Id, v.UserId, v.Title, v.Description, v.Category,
                v.ThumbnailUrl, v.Duration, v.ViewCount, v.CreatedAt,
                Username = v.User.Username, AvatarUrl = v.User.AvatarUrl
            })
            .ToListAsync();

        var items = rawItems.Select(v => new VodDto(
            v.Id, v.UserId, v.Username, v.AvatarUrl,
            v.Title, v.Description, v.Category, v.ThumbnailUrl,
            v.Duration, v.ViewCount, v.CreatedAt,
            HlsUrl: _cdn.VodUrl(v.UserId, v.Id)
        )).ToList();

        return Ok(new { items, total, page, pageSize });
    }

    /// <summary>Един VOD по ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var v = await _db.VodVideos
            .Include(x => x.User)
            .Include(x => x.Chapters)
            .FirstOrDefaultAsync(x => x.Id == id && x.Status == VodStatus.Ready && x.IsPublic);

        if (v is null) return NotFound();

        _ = Task.Run(async () =>
        {
            await _db.VodVideos.Where(x => x.Id == id)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.ViewCount, p => p.ViewCount + 1));
        });

        return Ok(new VodDetailDto(
            v.Id, v.UserId, v.User.Username, v.User.AvatarUrl,
            v.Title, v.Description, v.Category, v.ThumbnailUrl,
            v.Duration, v.ViewCount, v.CreatedAt,
            HlsUrl: _cdn.VodUrl(v.UserId, v.Id),
            Chapters: v.Chapters.OrderBy(c => c.Timestamp)
                .Select(c => new ChapterDto(c.Id, c.Title, c.Timestamp)).ToList()
        ));
    }

    /// <summary>Коментари под VOD</summary>
    [HttpGet("{id:int}/comments")]
    public async Task<IActionResult> GetComments(int id, [FromQuery] int page = 1)
    {
        const int pageSize = 30;
        var comments = await _db.VodComments
            .Where(c => c.VodVideoId == id && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new CommentDto(c.Id, c.UserId, c.Username, c.Content, c.Timestamp, c.CreatedAt))
            .ToListAsync();
        return Ok(comments);
    }

    /// <summary>Добавяне на коментар</summary>
    [HttpPost("{id:int}/comments")]
    [Authorize]
    public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentRequest req)
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;
        var user = await _db.Users.FindAsync(userId);
        if (user is null || user.IsBanned) return Forbid();

        var comment = new VodComment
        {
            VodVideoId = id,
            UserId = userId,
            Username = user.Username,
            Content = req.Content.Trim(),
            Timestamp = req.Timestamp
        };
        _db.VodComments.Add(comment);
        await _db.SaveChangesAsync();

        return Ok(new CommentDto(comment.Id, comment.UserId, comment.Username,
            comment.Content, comment.Timestamp, comment.CreatedAt));
    }

    // ── Authenticated endpoints ───────────────────────────────────────────────

    /// <summary>Запазване на прогрес на гледане</summary>
    [HttpPost("{id:int}/progress")]
    [Authorize]
    public async Task<IActionResult> SaveProgress(int id, [FromBody] ProgressRequest req)
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;

        var progress = await _db.WatchProgress
            .FirstOrDefaultAsync(w => w.VodVideoId == id && w.UserId == userId)
            ?? new WatchProgress { VodVideoId = id, UserId = userId };

        progress.Position = TimeSpan.FromSeconds(req.PositionSeconds);
        progress.Completed = req.Completed;
        progress.UpdatedAt = DateTime.UtcNow;

        if (progress.Id == 0) _db.WatchProgress.Add(progress);
        await _db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>Вземане на прогрес на гледане</summary>
    [HttpGet("{id:int}/progress")]
    [Authorize]
    public async Task<IActionResult> GetProgress(int id)
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;
        var p = await _db.WatchProgress
            .FirstOrDefaultAsync(w => w.VodVideoId == id && w.UserId == userId);
        return Ok(new { positionSeconds = p?.Position.TotalSeconds ?? 0, completed = p?.Completed ?? false });
    }

    // ── Streamer management ───────────────────────────────────────────────────

    /// <summary>Моите VOD видеа</summary>
    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMine([FromQuery] int page = 1)
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;
        const int pageSize = 20;

        var items = await _db.VodVideos
            .Where(v => v.UserId == userId && v.Status != VodStatus.Deleted)
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(v => new VodManageDto(
                v.Id, v.Title, v.Status.ToString(), v.Duration, v.ViewCount,
                v.IsPublic, v.CreatedAt, v.ProcessingError))
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>Обновяване на VOD метаданни</summary>
    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateVodRequest req)
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;
        var vod = await _db.VodVideos.FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);
        if (vod is null) return NotFound();

        vod.Title = req.Title;
        vod.Description = req.Description;
        vod.Category = req.Category;
        vod.IsPublic = req.IsPublic;
        await _db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>Изтриване на VOD</summary>
    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;
        var vod = await _db.VodVideos.FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);
        if (vod is null) return NotFound();
        vod.Status = VodStatus.Deleted;
        await _db.SaveChangesAsync();
        return Ok();
    }
}

// DTOs
public record VodDto(int Id, string UserId, string Username, string? AvatarUrl,
    string Title, string? Description, string? Category, string? ThumbnailUrl,
    TimeSpan? Duration, int ViewCount, DateTime CreatedAt, string HlsUrl);

public record VodDetailDto(int Id, string UserId, string Username, string? AvatarUrl,
    string Title, string? Description, string? Category, string? ThumbnailUrl,
    TimeSpan? Duration, int ViewCount, DateTime CreatedAt, string HlsUrl,
    List<ChapterDto> Chapters);

public record ChapterDto(int Id, string Title, TimeSpan Timestamp);
public record CommentDto(int Id, string UserId, string Username, string Content,
    TimeSpan? Timestamp, DateTime CreatedAt);

public record VodManageDto(int Id, string Title, string Status, TimeSpan? Duration,
    int ViewCount, bool IsPublic, DateTime CreatedAt, string? ProcessingError);

public record AddCommentRequest(string Content, TimeSpan? Timestamp = null);
public record ProgressRequest(double PositionSeconds, bool Completed = false);
public record UpdateVodRequest(string Title, string? Description, string? Category, bool IsPublic);
