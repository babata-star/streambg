using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StreamBG.Core.Entities;
using StreamBG.Infrastructure.Data;
using StreamBG.Infrastructure.Services;

namespace StreamBG.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly StreamBGDbContext _db;
    private readonly ICdnService _cdn;

    public SearchController(StreamBGDbContext db, ICdnService cdn)
    {
        _db = db;
        _cdn = cdn;
    }

    /// <summary>
    /// Унифицирано търсене по стриймове, потребители и VOD.
    /// type: all | streams | users | vod
    /// При type=all връща топ 5 резултата от всяко; при конкретен type — пагинирани резултати.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] string type = "all",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return BadRequest(new { error = "Търсачката изисква поне 2 символа" });

        q = q.Trim();
        pageSize = Math.Clamp(pageSize, 1, 50);
        type = type.ToLowerInvariant();
        if (type is not ("all" or "streams" or "users" or "vod")) type = "all";

        var (streams, streamsTotal) = await SearchStreamsAsync(q, type, page, pageSize);
        var (users, usersTotal)     = await SearchUsersAsync(q, type, page, pageSize);
        var (vod, vodTotal)         = await SearchVodAsync(q, type, page, pageSize);

        var total = type switch
        {
            "streams" => streamsTotal,
            "users"   => usersTotal,
            "vod"     => vodTotal,
            _         => streams.Count + users.Count + vod.Count
        };

        return Ok(new
        {
            Query = q, Type = type, Page = page, PageSize = pageSize, TotalCount = total,
            Streams = streams,
            Users   = users,
            Vod     = vod
        });
    }

    private async Task<(List<object> items, int total)> SearchStreamsAsync(
        string q, string type, int page, int pageSize)
    {
        if (type is not ("all" or "streams")) return (new(), 0);

        var query = _db.Streams.Include(s => s.User)
            .Where(s => s.IsLive && !s.User.IsBanned && (
                s.Title.Contains(q) ||
                (s.Description != null && s.Description.Contains(q)) ||
                s.User.Username.Contains(q)));

        int total = 0;
        IQueryable<Core.Entities.Stream> paged;

        if (type == "streams")
        {
            total = await query.CountAsync();
            paged = query.OrderByDescending(s => s.ViewerCount)
                .Skip((page - 1) * pageSize).Take(pageSize);
        }
        else
        {
            paged = query.OrderByDescending(s => s.ViewerCount).Take(5);
        }

        var items = await paged.Select(s => (object)new
        {
            s.Id, Username = s.User.Username, AvatarUrl = s.User.AvatarUrl,
            s.Title, s.Category, s.ViewerCount, s.IsLive, s.StartedAt, s.ThumbnailUrl,
            HlsUrl = _cdn.HlsUrl(s.StreamKey)
        }).ToListAsync();

        return (items, total);
    }

    private async Task<(List<object> items, int total)> SearchUsersAsync(
        string q, string type, int page, int pageSize)
    {
        if (type is not ("all" or "users")) return (new(), 0);

        var query = _db.Users.Where(u => !u.IsBanned && u.IsStreamer && (
            u.Username.Contains(q) ||
            (u.Bio != null && u.Bio.Contains(q))));

        int total = 0;
        List<ApplicationUser> users;

        if (type == "users")
        {
            total = await query.CountAsync();
            users = await query.OrderBy(u => u.Username)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        }
        else
        {
            users = await query.OrderBy(u => u.Username).Take(5).ToListAsync();
        }

        var userIds = users.Select(u => u.Id).ToList();
        var liveMap = await _db.Streams
            .Where(s => s.IsLive && userIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, s => s.ViewerCount);

        var items = users.Select(u => (object)new
        {
            u.Id, u.Username, u.AvatarUrl, u.Bio,
            IsLive = liveMap.ContainsKey(u.Id),
            CurrentViewers = liveMap.GetValueOrDefault(u.Id)
        }).ToList();

        return (items, total);
    }

    private async Task<(List<object> items, int total)> SearchVodAsync(
        string q, string type, int page, int pageSize)
    {
        if (type is not ("all" or "vod")) return (new(), 0);

        var query = _db.VodVideos.Include(v => v.User)
            .Where(v => v.IsPublic && v.Status == VodStatus.Ready && (
                v.Title.Contains(q) ||
                (v.Description != null && v.Description.Contains(q)) ||
                v.User.Username.Contains(q)));

        int total = 0;
        IQueryable<VodVideo> paged;

        if (type == "vod")
        {
            total = await query.CountAsync();
            paged = query.OrderByDescending(v => v.ViewCount)
                .Skip((page - 1) * pageSize).Take(pageSize);
        }
        else
        {
            paged = query.OrderByDescending(v => v.ViewCount).Take(5);
        }

        var items = await paged.Select(v => (object)new
        {
            v.Id, v.Title, v.ThumbnailUrl, v.Duration, v.ViewCount, v.CreatedAt, v.Category,
            Username = v.User.Username, AvatarUrl = v.User.AvatarUrl,
            HlsUrl = _cdn.VodUrl(v.UserId, v.Id)
        }).ToListAsync();

        return (items, total);
    }
}
