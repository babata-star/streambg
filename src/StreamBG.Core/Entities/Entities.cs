namespace StreamBG.Core.Entities;

public class ApplicationUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public bool IsStreamer { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsBanned { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Stream> Streams { get; set; } = new List<Stream>();
    public ICollection<Follow> Followers { get; set; } = new List<Follow>();
    public ICollection<Follow> Following { get; set; } = new List<Follow>();
}

public class Stream
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string StreamKey { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsLive { get; set; }
    public int ViewerCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ThumbnailUrl { get; set; }
    public int? CategoryId { get; set; }
    public Category? CategoryRef { get; set; }
    // Social relay settings
    public bool RelayToYouTube { get; set; }
    public bool RelayToFacebook { get; set; }
    public bool RelayToTikTok { get; set; }
    public string? YouTubeStreamKey { get; set; }
    public string? FacebookStreamKey { get; set; }
    public string? TikTokStreamKey { get; set; }
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}

public class ChatMessage
{
    public int Id { get; set; }
    public int StreamId { get; set; }
    public Stream Stream { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public class Follow
{
    public int Id { get; set; }
    public string FollowerId { get; set; } = string.Empty;
    public ApplicationUser Follower { get; set; } = null!;
    public string FolloweeId { get; set; } = string.Empty;
    public ApplicationUser Followee { get; set; } = null!;
    public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
    public bool NotificationsEnabled { get; set; } = true;
}

public class StreamAnalytics
{
    public int Id { get; set; }
    public int StreamId { get; set; }
    public int PeakViewers { get; set; }
    public int TotalUniqueViewers { get; set; }
    public int TotalChatMessages { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
