namespace StreamBG.Core.Entities;

public class VodVideo
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int? SourceStreamId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? ThumbnailUrl { get; set; }

    public string? RawFilePath { get; set; }
    public string? ProcessedFilePath { get; set; }
    public string? StorageKey { get; set; }

    public VodStatus Status { get; set; } = VodStatus.Pending;
    public string? ProcessingError { get; set; }

    public long FileSizeBytes { get; set; }
    public TimeSpan? Duration { get; set; }
    public int ViewCount { get; set; }
    public bool IsPublic { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    public ICollection<VodChapter> Chapters { get; set; } = new List<VodChapter>();
    public ICollection<VodComment> Comments { get; set; } = new List<VodComment>();
}

public enum VodStatus
{
    Pending,
    Processing,
    Ready,
    Failed,
    Deleted
}

public class VodChapter
{
    public int Id { get; set; }
    public int VodVideoId { get; set; }
    public VodVideo VodVideo { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public TimeSpan Timestamp { get; set; }
}

public class VodComment
{
    public int Id { get; set; }
    public int VodVideoId { get; set; }
    public VodVideo VodVideo { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public TimeSpan? Timestamp { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WatchProgress
{
    public int Id { get; set; }
    public int VodVideoId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public TimeSpan Position { get; set; }
    public bool Completed { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
