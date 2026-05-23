using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StreamBG.Core.Entities;
using StreamBG.Infrastructure.Data;
using StreamBG.Infrastructure.Services;

namespace StreamBG.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly StreamBGDbContext _db;
    private readonly ICdnService _cdn;

    public CategoriesController(StreamBGDbContext db, ICdnService cdn)
    {
        _db = db;
        _cdn = cdn;
    }

    /// <summary>Всички категории с брой активни стриймове и зрители</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var categories = await _db.Categories.OrderBy(c => c.SortOrder).ToListAsync();

        var liveStats = await _db.Streams
            .Where(s => s.IsLive && s.CategoryId != null)
            .GroupBy(s => s.CategoryId)
            .Select(g => new { CatId = g.Key, Count = g.Count(), Viewers = g.Sum(s => s.ViewerCount) })
            .ToListAsync();

        var statsMap = liveStats.ToDictionary(x => x.CatId!.Value, x => (x.Count, x.Viewers));

        var result = categories.Select(c =>
        {
            var (count, viewers) = statsMap.GetValueOrDefault(c.Id);
            return new
            {
                c.Id, c.Name, c.Slug, c.Icon, c.Description, c.Color,
                LiveStreamCount = count,
                TotalViewers = viewers
            };
        });

        return Ok(result);
    }

    /// <summary>Категория по slug с активни стриймове</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == slug);
        if (cat is null) return NotFound();

        var streamQuery = _db.Streams.Include(s => s.User)
            .Where(s => s.CategoryId == cat.Id && s.IsLive && !s.User.IsBanned);

        var total = await streamQuery.CountAsync();
        var streams = await streamQuery
            .OrderByDescending(s => s.ViewerCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id, Username = s.User.Username, AvatarUrl = s.User.AvatarUrl,
                s.Title, s.Category, s.ViewerCount, s.IsLive, s.StartedAt, s.ThumbnailUrl,
                HlsUrl = _cdn.HlsUrl(s.StreamKey)
            })
            .ToListAsync();

        return Ok(new
        {
            cat.Id, cat.Name, cat.Slug, cat.Icon, cat.Description, cat.Color,
            LiveStreamCount = total,
            TotalViewers = streams.Sum(s => s.ViewerCount),
            Streams = streams,
            Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    /// <summary>VOD видеа в дадена категория</summary>
    [HttpGet("{slug}/vod")]
    public async Task<IActionResult> GetVod(
        string slug,
        [FromQuery] string sort = "popular",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == slug);
        if (cat is null) return NotFound();

        var query = _db.VodVideos.Include(v => v.User)
            .Where(v => v.Category == cat.Name && v.IsPublic && v.Status == VodStatus.Ready);

        IQueryable<VodVideo> sorted = sort switch
        {
            "newest" => query.OrderByDescending(v => v.CreatedAt),
            "oldest" => query.OrderBy(v => v.CreatedAt),
            _        => query.OrderByDescending(v => v.ViewCount)
        };

        var total = await query.CountAsync();
        var items = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new
            {
                v.Id, v.Title, v.ThumbnailUrl, v.Duration, v.ViewCount, v.CreatedAt,
                Username = v.User.Username, AvatarUrl = v.User.AvatarUrl,
                HlsUrl = _cdn.VodUrl(v.UserId, v.Id)
            })
            .ToListAsync();

        return Ok(new { Items = items, TotalCount = total, Page = page, PageSize = pageSize });
    }
}
