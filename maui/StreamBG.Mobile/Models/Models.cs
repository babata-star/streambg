namespace StreamBG.Mobile.Models;

public record AuthResult(bool Success, string? AccessToken, string? RefreshToken, User? User, string? Error);

public record User(string Id, string Username, string? AvatarUrl, bool IsStreamer, bool IsAdmin);

public record LiveStream(
    int Id, string UserId, string Username, string? AvatarUrl,
    string Title, string? Category, string HlsUrl,
    int ViewerCount, bool IsLive, DateTime? StartedAt, string? ThumbnailUrl);

public record VodVideo(
    int Id, string UserId, string Username, string? AvatarUrl,
    string Title, string? Description, string? Category, string? ThumbnailUrl,
    TimeSpan? Duration, int ViewCount, DateTime CreatedAt, string HlsUrl);

public record ChatMessage(
    int Id, string UserId, string Username, string? AvatarUrl,
    string Content, string Color, DateTime SentAt);

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public record SearchUserResult(
    string Id, string Username, string? AvatarUrl, string? Bio,
    bool IsLive, int CurrentViewers);

public record SearchResults(
    string Query, string Type,
    List<LiveStream> Streams,
    List<SearchUserResult> Users,
    List<VodVideo> Vod,
    int TotalCount);

public record Clip(
    int      Id,
    string   CreatorUsername,
    string?  CreatorAvatarUrl,
    string   StreamerUsername,
    string?  StreamerAvatarUrl,
    int?     SourceStreamId,
    int?     SourceVodId,
    string   Title,
    int      DurationSeconds,
    double   OffsetSeconds,
    string?  VideoUrl,
    string?  ThumbnailUrl,
    int      ViewCount,
    string   Status,          // "Pending" | "Processing" | "Ready" | "Failed"
    DateTime CreatedAt);

public record CreateClipRequest(int? StreamId, int? VodId, double OffsetSeconds, string? Title);

public static class AppColors
{
    public const string Accent     = "#a970ff";
    public const string AccentDark = "#7c3aed";
    public const string BgBase     = "#0e0e10";
    public const string BgSurface  = "#18181b";
    public const string BgElevated = "#1f1f23";
    public const string LiveRed    = "#eb0400";
    public const string TextPrimary   = "#efeff1";
    public const string TextSecondary = "#adadb8";
    public const string TextMuted     = "#5a5a6a";
    public const string Success    = "#00c853";
}
