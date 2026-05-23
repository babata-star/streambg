using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using StreamBG.API.Controllers;
using StreamBG.Core.Entities;
using StreamBG.Infrastructure.Data;
using StreamBG.Infrastructure.Services;

namespace StreamBG.API.Services;

// ── Interfaces ────────────────────────────────────────────────────────────────

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest req);
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(string userId, string? refreshToken = null);
    Task<UserDto?> GetCurrentUserAsync(string userId);
}

public interface IStreamService
{
    Task<bool> OnStreamPublishAsync(string streamKey, string? ip);
    Task OnStreamEndAsync(string streamKey);
    Task<PagedResult<StreamDto>> GetLiveStreamsAsync(string? category, int page, int pageSize);
    Task<StreamDto?> GetStreamByUsernameAsync(string username);
    Task<ServiceResult> UpdateStreamSettingsAsync(string userId, StreamSettingsRequest req);
    Task<ServiceResult> UpdateSocialRelayAsync(string userId, SocialRelayRequest req);
    Task<string> GetStreamKeyAsync(string userId);
    Task<string> RegenerateStreamKeyAsync(string userId);
    Task<PagedResult<StreamHistoryDto>> GetStreamHistoryAsync(string userId, int page);
    Task<List<string>> GetCategoriesAsync();
    Task<PlatformStats> GetPlatformStatsAsync();
    Task<List<StreamDto>> GetAllLiveStreamsAdminAsync();
    Task TerminateStreamAsync(int streamId);
    Task DeleteChatMessageAsync(int messageId);
    /// <summary>
    /// Called by the nginx-rtmp on_record_done webhook. Creates a VodVideo record
    /// and returns its ID so VodTranscodingService can start processing it.
    /// Returns 0 if the stream key is unknown or the file doesn't exist.
    /// </summary>
    Task<int> CreateVodFromRecordingAsync(string streamKey, string filePath);
}

public interface IChatService
{
    Task<ChatMessage?> SaveMessageAsync(string streamId, string userId, string content);
    Task<bool> CanDeleteMessageAsync(string userId, int messageId);
    Task DeleteMessageAsync(int messageId);
    Task<List<ChatMessageDto>> GetRecentMessagesAsync(string streamId, int count = 50);
}

public interface IUserService
{
    Task<PagedResult<UserAdminDto>> GetAllUsersAsync(string? search, int page, int pageSize);
    Task BanUserAsync(string userId, string reason);
    Task UnbanUserAsync(string userId);
    Task SetStreamerRoleAsync(string userId, bool isStreamer);
}

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user);
    /// <summary>Generates a refresh token and stores it in Redis.</summary>
    Task<string> GenerateRefreshTokenAsync(string userId);
    /// <summary>Validates a refresh token from Redis. Returns null if invalid/expired.</summary>
    Task<ClaimsPrincipal?> ValidateRefreshTokenAsync(string token);
}

public interface IViewerCountService
{
    Task IncrementAsync(string streamId);
    Task DecrementAsync(string streamId);
    Task<int> GetCountAsync(string streamId);
    /// <summary>Registers a connection-to-stream mapping so disconnect can decrement the correct counter.</summary>
    void TrackConnection(string connectionId, string streamId);
    /// <summary>Removes the connection mapping and decrements the count. Returns the streamId if tracked, else null.</summary>
    Task<string?> OnDisconnectAsync(string connectionId);
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record AuthResult(bool Success, string? AccessToken = null, string? RefreshToken = null,
    UserDto? User = null, string? Error = null);
public record ServiceResult(bool Success, string? Error = null, object? Data = null);

public record UserDto(string Id, string Username, string? AvatarUrl, bool IsStreamer, bool IsAdmin);
public record UserAdminDto(string Id, string Username, string Email, bool IsStreamer, bool IsAdmin,
    bool IsBanned, DateTime CreatedAt);

public record StreamDto(int Id, string UserId, string Username, string? AvatarUrl, string Title,
    string? Category, string HlsUrl, int ViewerCount, bool IsLive, DateTime? StartedAt,
    string? ThumbnailUrl);

public record StreamHistoryDto(int Id, string Title, DateTime? StartedAt, DateTime? EndedAt,
    int PeakViewers, TimeSpan Duration);

public record ChatMessageDto(int Id, string UserId, string Username, string? AvatarUrl,
    string Content, string? Color, DateTime SentAt);

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

public record PlatformStats(int TotalUsers, int ActiveStreams, int TotalViewers,
    int StreamsToday, int NewUsersToday);

// ── Token Service ─────────────────────────────────────────────────────────────

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly IDistributedCache _cache;

    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public TokenService(IConfiguration config, IDistributedCache cache)
    {
        _config = config;
        _cache = cache;
    }

    public string GenerateAccessToken(ApplicationUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("username", user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (user.IsAdmin) claims.Add(new Claim("role", "admin"));
        else if (user.IsStreamer) claims.Add(new Claim("role", "streamer"));
        else claims.Add(new Claim("role", "viewer"));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(double.Parse(_config["Jwt:ExpiryHours"] ?? "24")),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> GenerateRefreshTokenAsync(string userId)
    {
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var key = $"refresh:{token}";

        await _cache.SetStringAsync(key, userId, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = RefreshTokenLifetime
        });

        return token;
    }

    public async Task<ClaimsPrincipal?> ValidateRefreshTokenAsync(string token)
    {
        var key = $"refresh:{token}";
        var userId = await _cache.GetStringAsync(key);

        if (string.IsNullOrEmpty(userId))
            return null;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var identity = new ClaimsIdentity(claims, "refresh");
        return new ClaimsPrincipal(identity);
    }


}

// ── Viewer Count Service (Redis-backed) ───────────────────────────────────────

public class ViewerCountService : IViewerCountService
{
    private readonly IDistributedCache _cache;
    // connectionId -> streamId mapping for disconnect handling
    private readonly ConcurrentDictionary<string, string> _connMap = new();

    public ViewerCountService(IDistributedCache cache) => _cache = cache;

    public void TrackConnection(string connectionId, string streamId)
    {
        _connMap[connectionId] = streamId;
    }

    public async Task IncrementAsync(string streamId)
    {
        var key = $"viewers:{streamId}";
        var val = await _cache.GetStringAsync(key);
        var count = int.TryParse(val, out var n) ? n : 0;
        await _cache.SetStringAsync(key, (count + 1).ToString(),
            new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(12) });
    }

    public async Task DecrementAsync(string streamId)
    {
        var key = $"viewers:{streamId}";
        var val = await _cache.GetStringAsync(key);
        var count = int.TryParse(val, out var n) ? Math.Max(0, n - 1) : 0;
        await _cache.SetStringAsync(key, count.ToString(),
            new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(12) });
    }

    public async Task<int> GetCountAsync(string streamId)
    {
        var val = await _cache.GetStringAsync($"viewers:{streamId}");
        return int.TryParse(val, out var n) ? n : 0;
    }

    public async Task<string?> OnDisconnectAsync(string connectionId)
    {
        if (_connMap.TryRemove(connectionId, out var streamId))
        {
            await DecrementAsync(streamId);
            return streamId;
        }
        return null;
    }
}

// ── Auth Service ──────────────────────────────────────────────────────────────

public class AuthService : IAuthService
{
    private readonly StreamBGDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IDistributedCache _cache;

    public AuthService(StreamBGDbContext db, ITokenService tokens, IDistributedCache cache)
    {
        _db = db;
        _tokens = tokens;
        _cache = cache;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return new AuthResult(false, Error: "Имейлът вече е регистриран");

        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
            return new AuthResult(false, Error: "Потребителското име е заето");

        var user = new ApplicationUser
        {
            Username = req.Username.Trim(),
            Email = req.Email.ToLowerInvariant().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Bio = req.Bio
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var accessToken = _tokens.GenerateAccessToken(user);
        var refreshToken = await _tokens.GenerateRefreshTokenAsync(user.Id);

        return new AuthResult(true, accessToken, refreshToken,
            new UserDto(user.Id, user.Username, user.AvatarUrl, user.IsStreamer, user.IsAdmin));
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return new AuthResult(false, Error: "Грешен имейл или парола");

        if (user.IsBanned)
            return new AuthResult(false, Error: "Акаунтът е блокиран");

        var accessToken = _tokens.GenerateAccessToken(user);
        var refreshToken = await _tokens.GenerateRefreshTokenAsync(user.Id);

        return new AuthResult(true, accessToken, refreshToken,
            new UserDto(user.Id, user.Username, user.AvatarUrl, user.IsStreamer, user.IsAdmin));
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
    {
        var principal = await _tokens.ValidateRefreshTokenAsync(refreshToken);
        if (principal is null)
            return new AuthResult(false, Error: "Невалиден или изтекъл refresh token");

        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userId))
            return new AuthResult(false, Error: "Невалиден token");

        var user = await _db.Users.FindAsync(userId);
        if (user is null || user.IsBanned)
            return new AuthResult(false, Error: "Потребителят не е намерен или е блокиран");

        // Invalidate old refresh token (rotation)
        await InvalidateRefreshTokenAsync(refreshToken);

        var newAccessToken = _tokens.GenerateAccessToken(user);
        var newRefreshToken = await _tokens.GenerateRefreshTokenAsync(user.Id);

        return new AuthResult(true, newAccessToken, newRefreshToken,
            new UserDto(user.Id, user.Username, user.AvatarUrl, user.IsStreamer, user.IsAdmin));
    }

    public async Task LogoutAsync(string userId, string? refreshToken = null)
    {
        if (!string.IsNullOrEmpty(refreshToken))
            await InvalidateRefreshTokenAsync(refreshToken);
    }

    private async Task InvalidateRefreshTokenAsync(string token)
    {
        var key = $"refresh:{token}";
        await _cache.RemoveAsync(key);
    }

    public async Task<UserDto?> GetCurrentUserAsync(string userId)
    {
        var user = await _db.Users.FindAsync(userId);
        return user is null ? null
            : new UserDto(user.Id, user.Username, user.AvatarUrl, user.IsStreamer, user.IsAdmin);
    }
}

// ── Stream Service ────────────────────────────────────────────────────────────

public class StreamService : IStreamService
{
    private readonly StreamBGDbContext _db;
    private readonly IConfiguration _config;
    private readonly ICdnService _cdn;

    public StreamService(StreamBGDbContext db, IConfiguration config, ICdnService cdn)
    {
        _db = db;
        _config = config;
        _cdn = cdn;
    }

    public async Task<bool> OnStreamPublishAsync(string streamKey, string? ip)
    {
        var stream = await _db.Streams
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StreamKey == streamKey);

        if (stream is null || stream.User.IsBanned) return false;

        stream.IsLive = true;
        stream.StartedAt = DateTime.UtcNow;
        stream.ViewerCount = 0;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task OnStreamEndAsync(string streamKey)
    {
        var stream = await _db.Streams.FirstOrDefaultAsync(s => s.StreamKey == streamKey);
        if (stream is null) return;

        stream.IsLive = false;
        stream.EndedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Purge stale live segments from CDN after stream ends
        await _cdn.InvalidateAsync($"/hls/{streamKey}/*");
    }

    public async Task<PagedResult<StreamDto>> GetLiveStreamsAsync(string? category, int page, int pageSize)
    {
        var query = _db.Streams.Include(s => s.User)
            .Where(s => s.IsLive && !s.User.IsBanned);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(s => s.Category == category);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.ViewerCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => ToDto(s))
            .ToListAsync();

        return new PagedResult<StreamDto>(items, total, page, pageSize);
    }

    public async Task<StreamDto?> GetStreamByUsernameAsync(string username)
    {
        var stream = await _db.Streams
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.User.Username == username && s.IsLive);
        return stream is null ? null : ToDto(stream);
    }

    public async Task<ServiceResult> UpdateStreamSettingsAsync(string userId, StreamSettingsRequest req)
    {
        var stream = await _db.Streams.FirstOrDefaultAsync(s => s.UserId == userId)
            ?? new Core.Entities.Stream { UserId = userId };

        stream.Title = req.Title;
        stream.Description = req.Description;
        stream.Category = req.Category;
        stream.CategoryId = string.IsNullOrEmpty(req.Category) ? null
            : (await _db.Categories.FirstOrDefaultAsync(c => c.Name == req.Category))?.Id;
        stream.RelayToYouTube = req.RelayToYouTube;
        stream.RelayToFacebook = req.RelayToFacebook;
        stream.RelayToTikTok = req.RelayToTikTok;

        if (stream.Id == 0) _db.Streams.Add(stream);
        await _db.SaveChangesAsync();
        return new ServiceResult(true);
    }

    public async Task<ServiceResult> UpdateSocialRelayAsync(string userId, SocialRelayRequest req)
    {
        var stream = await _db.Streams.FirstOrDefaultAsync(s => s.UserId == userId);
        if (stream is null) return new ServiceResult(false, "Нямате настроен стрийм");

        stream.YouTubeStreamKey = req.YouTubeStreamKey;
        stream.FacebookStreamKey = req.FacebookStreamKey;
        stream.TikTokStreamKey = req.TikTokStreamKey;
        stream.RelayToYouTube = req.RelayToYouTube;
        stream.RelayToFacebook = req.RelayToFacebook;
        stream.RelayToTikTok = req.RelayToTikTok;

        await _db.SaveChangesAsync();
        return new ServiceResult(true);
    }

    public async Task<string> GetStreamKeyAsync(string userId)
    {
        var stream = await _db.Streams.FirstOrDefaultAsync(s => s.UserId == userId);
        if (stream is not null) return stream.StreamKey;

        // Автоматично създай стрийм запис при първо достъпване
        var newStream = new Core.Entities.Stream { UserId = userId, Title = "Моят стрийм" };
        _db.Streams.Add(newStream);
        await _db.SaveChangesAsync();
        return newStream.StreamKey;
    }

    public async Task<string> RegenerateStreamKeyAsync(string userId)
    {
        var stream = await _db.Streams.FirstOrDefaultAsync(s => s.UserId == userId)
            ?? new Core.Entities.Stream { UserId = userId };

        stream.StreamKey = Guid.NewGuid().ToString("N");
        if (stream.Id == 0) _db.Streams.Add(stream);
        await _db.SaveChangesAsync();
        return stream.StreamKey;
    }

    public async Task<PagedResult<StreamHistoryDto>> GetStreamHistoryAsync(string userId, int page)
    {
        const int pageSize = 20;
        var query = _db.Streams.Where(s => s.UserId == userId && !s.IsLive);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StreamHistoryDto(s.Id, s.Title, s.StartedAt, s.EndedAt, 0,
                s.StartedAt.HasValue && s.EndedAt.HasValue
                    ? s.EndedAt.Value - s.StartedAt.Value
                    : TimeSpan.Zero))
            .ToListAsync();
        return new PagedResult<StreamHistoryDto>(items, total, page, pageSize);
    }

    public Task<List<string>> GetCategoriesAsync() =>
        _db.Categories.OrderBy(c => c.SortOrder).Select(c => c.Name).ToListAsync();

    public async Task<PlatformStats> GetPlatformStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        return new PlatformStats(
            TotalUsers: await _db.Users.CountAsync(),
            ActiveStreams: await _db.Streams.CountAsync(s => s.IsLive),
            TotalViewers: await _db.Streams.Where(s => s.IsLive).SumAsync(s => s.ViewerCount),
            StreamsToday: await _db.Streams.CountAsync(s => s.StartedAt >= today),
            NewUsersToday: await _db.Users.CountAsync(u => u.CreatedAt >= today)
        );
    }

    public async Task<List<StreamDto>> GetAllLiveStreamsAdminAsync() =>
        await _db.Streams.Include(s => s.User)
            .Where(s => s.IsLive)
            .Select(s => ToDto(s))
            .ToListAsync();

    public async Task TerminateStreamAsync(int streamId)
    {
        var s = await _db.Streams.FindAsync(streamId);
        if (s is null) return;
        s.IsLive = false;
        s.EndedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteChatMessageAsync(int messageId)
    {
        var msg = await _db.ChatMessages.FindAsync(messageId);
        if (msg is null) return;
        msg.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task<int> CreateVodFromRecordingAsync(string streamKey, string filePath)
    {
        var stream = await _db.Streams
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StreamKey == streamKey);

        if (stream is null)
        {
            // Stream key no longer in DB — nothing to do
            return 0;
        }

        if (!File.Exists(filePath))
        {
            // File path reported by nginx-rtmp is not accessible from this container.
            // This can happen if the recordings volume is not mounted on the API container.
            // Log and return; the admin can re-trigger manually.
            return 0;
        }

        var vod = new VodVideo
        {
            UserId         = stream.UserId,
            SourceStreamId = stream.Id,
            Title          = stream.Title,
            Category       = stream.Category,
            ThumbnailUrl   = stream.ThumbnailUrl,
            RawFilePath    = filePath,
            Status         = VodStatus.Pending,
            FileSizeBytes  = new FileInfo(filePath).Length,
            IsPublic       = true,
        };

        _db.VodVideos.Add(vod);
        await _db.SaveChangesAsync();
        return vod.Id;
    }

    private StreamDto ToDto(Core.Entities.Stream s) => new(
        s.Id, s.UserId, s.User.Username, s.User.AvatarUrl, s.Title, s.Category,
        HlsUrl: _cdn.HlsUrl(s.StreamKey),
        s.ViewerCount, s.IsLive, s.StartedAt,
        ThumbnailUrl: _cdn.ThumbnailUrl(s.ThumbnailUrl));
}

// ── Chat Service ──────────────────────────────────────────────────────────────

public class ChatService : IChatService
{
    private readonly StreamBGDbContext _db;

    public ChatService(StreamBGDbContext db) => _db = db;

    public async Task<ChatMessage?> SaveMessageAsync(string streamId, string userId, string content)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null || user.IsBanned) return null;

        if (!int.TryParse(streamId, out var sid)) return null;

        var msg = new ChatMessage
        {
            StreamId = sid,
            UserId = userId,
            Username = user.Username,
            AvatarUrl = user.AvatarUrl,
            Content = content.Trim(),
            Color = GetUserColor(userId)
        };
        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync();
        return msg;
    }

    public async Task<bool> CanDeleteMessageAsync(string userId, int messageId)
    {
        var user = await _db.Users.FindAsync(userId);
        return user?.IsAdmin == true;
    }

    public async Task DeleteMessageAsync(int messageId)
    {
        var msg = await _db.ChatMessages.FindAsync(messageId);
        if (msg is null) return;
        msg.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task<List<ChatMessageDto>> GetRecentMessagesAsync(string streamId, int count = 50)
    {
        if (!int.TryParse(streamId, out var sid)) return new();

        return await _db.ChatMessages
            .Where(m => m.StreamId == sid && !m.IsDeleted)
            .OrderByDescending(m => m.SentAt)
            .Take(count)
            .OrderBy(m => m.SentAt)
            .Select(m => new ChatMessageDto(m.Id, m.UserId, m.Username, m.AvatarUrl,
                m.Content, m.Color, m.SentAt))
            .ToListAsync();
    }

    // Генерира постоянен цвят за потребителя в чата (като Twitch)
    private static string GetUserColor(string userId)
    {
        var colors = new[] { "#FF4444", "#FF8C00", "#FFD700", "#32CD32", "#00CED1",
                             "#1E90FF", "#DA70D6", "#FF69B4", "#7B68EE", "#20B2AA" };
        var hash = Math.Abs(userId.GetHashCode());
        return colors[hash % colors.Length];
    }
}

// ── User Service ──────────────────────────────────────────────────────────────

public class UserService : IUserService
{
    private readonly StreamBGDbContext _db;

    public UserService(StreamBGDbContext db) => _db = db;

    public async Task<PagedResult<UserAdminDto>> GetAllUsersAsync(string? search, int page, int pageSize)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.Username.Contains(search) || u.Email.Contains(search));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserAdminDto(u.Id, u.Username, u.Email, u.IsStreamer, u.IsAdmin,
                u.IsBanned, u.CreatedAt))
            .ToListAsync();
        return new PagedResult<UserAdminDto>(items, total, page, pageSize);
    }

    public async Task BanUserAsync(string userId, string reason)
    {
        var u = await _db.Users.FindAsync(userId);
        if (u is null) return;
        u.IsBanned = true;
        await _db.SaveChangesAsync();
    }

    public async Task UnbanUserAsync(string userId)
    {
        var u = await _db.Users.FindAsync(userId);
        if (u is null) return;
        u.IsBanned = false;
        await _db.SaveChangesAsync();
    }

    public async Task SetStreamerRoleAsync(string userId, bool isStreamer)
    {
        var u = await _db.Users.FindAsync(userId);
        if (u is null) return;
        u.IsStreamer = isStreamer;
        await _db.SaveChangesAsync();
    }
}
