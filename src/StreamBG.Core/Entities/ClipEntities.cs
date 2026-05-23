namespace StreamBG.Core.Entities;

/// <summary>
/// A short (default 30 s) video clip created by a viewer or streamer from a
/// live stream or a VOD.  Stored as a single MP4 file so it can be shared and
/// played without an HLS player.
/// </summary>
public class Clip
{
    public int    Id { get; set; }

    // ── Authorship ────────────────────────────────────────────────────────────
    /// <summary>User who created the clip.</summary>
    public string          CreatorUserId { get; set; } = string.Empty;
    public ApplicationUser Creator       { get; set; } = null!;

    /// <summary>User whose stream/VOD was clipped.</summary>
    public string          StreamerUserId { get; set; } = string.Empty;
    public ApplicationUser Streamer       { get; set; } = null!;

    // ── Source ────────────────────────────────────────────────────────────────
    /// <summary>Live stream the clip was taken from (null for VOD clips).</summary>
    public int? SourceStreamId { get; set; }

    /// <summary>VOD the clip was taken from (null for live clips).</summary>
    public int? SourceVodId { get; set; }

    // ── Content ───────────────────────────────────────────────────────────────
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Start offset in the source recording (seconds).
    /// For live clips this is computed at request time (stream elapsed time - DurationSeconds).
    /// </summary>
    public double OffsetSeconds   { get; set; }
    public int    DurationSeconds { get; set; } = 30;

    // ── Output files ──────────────────────────────────────────────────────────
    /// <summary>Absolute container path, e.g. /data/clips/42.mp4</summary>
    public string? FilePath      { get; set; }
    /// <summary>Absolute container path, e.g. /data/clips/42.jpg</summary>
    public string? ThumbnailPath { get; set; }

    // ── Processing ────────────────────────────────────────────────────────────
    public ClipStatus Status          { get; set; } = ClipStatus.Pending;
    public string?    ProcessingError { get; set; }

    // ── Stats ─────────────────────────────────────────────────────────────────
    public int  ViewCount { get; set; }
    public bool IsPublic  { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ClipStatus
{
    Pending,
    Processing,
    Ready,
    Failed
}
