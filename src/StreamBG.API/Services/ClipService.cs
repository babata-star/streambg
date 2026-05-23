using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using StreamBG.Core.Entities;
using StreamBG.Infrastructure.Data;
using StreamBG.Infrastructure.Services;

namespace StreamBG.API.Services;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ClipDto(
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
    ClipStatus Status,
    DateTime CreatedAt);

/// <summary>
/// Request body for POST /api/clips.
/// Provide either <see cref="StreamId"/> (live clip) or <see cref="VodId"/> (VOD clip), not both.
/// </summary>
public record CreateClipRequest(
    int?    StreamId,
    int?    VodId,
    /// <summary>
    /// For VOD clips: start of the 30-second window (seconds from the beginning).
    /// For live clips: ignored — always captures the most recent 30 seconds.
    /// </summary>
    double  OffsetSeconds,
    string? Title);

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IClipService
{
    Task<(int ClipId, string? Error)> CreateClipAsync(string userId, CreateClipRequest req);
    Task<ClipDto?>                    GetClipAsync(int clipId);
    Task<PagedResult<ClipDto>>        GetClipsAsync(
        string? streamerId, string? category, string sort, int page, int pageSize);
    Task<List<ClipDto>>               GetStreamerClipsAsync(string streamerId, int page, int pageSize);
    Task IncrementViewAsync(int clipId);
    Task<bool> DeleteClipAsync(string userId, bool isAdmin, int clipId);
}

// ── Service ───────────────────────────────────────────────────────────────────

public sealed class ClipService : IClipService
{
    private readonly StreamBGDbContext _db;
    private readonly ICdnService       _cdn;
    private readonly Channel<int>      _queue;   // clip-specific queue

    public ClipService(StreamBGDbContext db, ICdnService cdn, Channel<int> clipQueue)
    {
        _db    = db;
        _cdn   = cdn;
        _queue = clipQueue;
    }

    public async Task<(int ClipId, string? Error)> CreateClipAsync(
        string userId, CreateClipRequest req)
    {
        // ── Validate source ───────────────────────────────────────────────────
        if (req.StreamId is null && req.VodId is null)
            return (0, "Изисква се StreamId или VodId");
        if (req.StreamId is not null && req.VodId is not null)
            return (0, "Задайте само един от StreamId или VodId");

        var creator = await _db.Users.FindAsync(userId);
        if (creator is null || creator.IsBanned)
            return (0, "Потребителят не е намерен или е блокиран");

        // ── Live clip ─────────────────────────────────────────────────────────
        if (req.StreamId is not null)
        {
            var stream = await _db.Streams
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == req.StreamId);

            if (stream is null) return (0, "Стриймът не е намерен");
            if (!stream.IsLive)  return (0, "Стриймът не е на живо в момента");
            if (stream.StartedAt is null) return (0, "Стриймът няма StartedAt");

            // Compute clip start = stream elapsed time - 30 s
            var elapsed = (DateTime.UtcNow - stream.StartedAt.Value).TotalSeconds;
            if (elapsed < 30)
                return (0, "Стриймът трябва да е поне 30 секунди, преди да може да се клипира");

            var title = string.IsNullOrWhiteSpace(req.Title)
                ? $"{stream.User.Username} – {DateTime.UtcNow:HH:mm dd.MM.yyyy}"
                : req.Title.Trim();

            var clip = new Clip
            {
                CreatorUserId  = userId,
                StreamerUserId = stream.UserId,
                SourceStreamId = stream.Id,
                Title          = title,
                OffsetSeconds  = Math.Max(0, elapsed - 30),
                DurationSeconds = 30,
            };
            _db.Clips.Add(clip);
            await _db.SaveChangesAsync();

            await _queue.Writer.WriteAsync(clip.Id);
            return (clip.Id, null);
        }

        // ── VOD clip ──────────────────────────────────────────────────────────
        {
            var vod = await _db.VodVideos
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.Id == req.VodId);

            if (vod is null)              return (0, "VOD не е намерен");
            if (vod.Status != VodStatus.Ready) return (0, "VOD все още се обработва");
            if (!vod.IsPublic && vod.UserId != userId)
                return (0, "Нямате достъп до този VOD");

            var totalSeconds = vod.Duration?.TotalSeconds ?? double.MaxValue;
            var offset       = req.OffsetSeconds;

            if (offset < 0)           return (0, "OffsetSeconds не може да е отрицателен");
            if (offset + 30 > totalSeconds)
                return (0, $"Отместването + 30 с надвишава продължителността на VOD ({totalSeconds:F0} с)");

            var title = string.IsNullOrWhiteSpace(req.Title)
                ? $"{vod.User.Username} – {TimeSpan.FromSeconds(offset):mm\\:ss}"
                : req.Title.Trim();

            var clip = new Clip
            {
                CreatorUserId   = userId,
                StreamerUserId  = vod.UserId,
                SourceVodId     = vod.Id,
                Title           = title,
                OffsetSeconds   = offset,
                DurationSeconds = 30,
            };
            _db.Clips.Add(clip);
            await _db.SaveChangesAsync();

            await _queue.Writer.WriteAsync(clip.Id);
            return (clip.Id, null);
        }
    }

    public async Task<ClipDto?> GetClipAsync(int clipId)
    {
        var c = await _db.Clips
            .Include(x => x.Creator)
            .Include(x => x.Streamer)
            .FirstOrDefaultAsync(x => x.Id == clipId && x.IsPublic);
        return c is null ? null : ToDto(c);
    }

    public async Task<PagedResult<ClipDto>> GetClipsAsync(
        string? streamerId, string? category, string sort, int page, int pageSize)
    {
        var q = _db.Clips
            .Include(c => c.Creator)
            .Include(c => c.Streamer)
            .Where(c => c.IsPublic && c.Status == ClipStatus.Ready);

        if (!string.IsNullOrEmpty(streamerId))
            q = q.Where(c => c.StreamerUserId == streamerId);

        q = sort switch
        {
            "popular" => q.OrderByDescending(c => c.ViewCount),
            _         => q.OrderByDescending(c => c.CreatedAt),
        };

        var total = await q.CountAsync();
        var items = await q
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => ToDto(c))
            .ToListAsync();

        return new PagedResult<ClipDto>(items, total, page, pageSize);
    }

    public async Task<List<ClipDto>> GetStreamerClipsAsync(string streamerId, int page, int pageSize)
    {
        return await _db.Clips
            .Include(c => c.Creator)
            .Include(c => c.Streamer)
            .Where(c => c.StreamerUserId == streamerId && c.IsPublic && c.Status == ClipStatus.Ready)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => ToDto(c))
            .ToListAsync();
    }

    public async Task IncrementViewAsync(int clipId)
    {
        await _db.Clips
            .Where(c => c.Id == clipId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ViewCount, c => c.ViewCount + 1));
    }

    public async Task<bool> DeleteClipAsync(string userId, bool isAdmin, int clipId)
    {
        var clip = await _db.Clips.FindAsync(clipId);
        if (clip is null) return false;
        if (!isAdmin && clip.CreatorUserId != userId) return false;

        clip.IsPublic = false;
        clip.Status   = ClipStatus.Failed;   // hide from public queries
        await _db.SaveChangesAsync();

        // Best-effort file cleanup
        try { if (clip.FilePath      is not null) File.Delete(clip.FilePath);      } catch { }
        try { if (clip.ThumbnailPath is not null) File.Delete(clip.ThumbnailPath); } catch { }

        return true;
    }

    private ClipDto ToDto(Clip c) => new(
        c.Id,
        c.Creator.Username,
        c.Creator.AvatarUrl,
        c.Streamer.Username,
        c.Streamer.AvatarUrl,
        c.SourceStreamId,
        c.SourceVodId,
        c.Title,
        c.DurationSeconds,
        c.OffsetSeconds,
        c.Status == ClipStatus.Ready ? _cdn.ClipUrl(c.Id) : null,
        c.Status == ClipStatus.Ready ? _cdn.ClipThumbnailUrl(c.Id) : null,
        c.ViewCount,
        c.Status,
        c.CreatedAt);
}

// ── Background transcoding service ────────────────────────────────────────────

/// <summary>
/// Reads clip IDs from a <see cref="Channel{Int32}"/>, finds the source recording
/// or VOD raw file, and extracts a 30-second MP4 via FFmpeg.
///
/// Live clips  → reads the raw FLV recording for the stream, seeks to OffsetSeconds.
/// VOD clips   → reads the VOD raw FLV (or falls back to the processed 720p HLS).
/// Thumbnail   → extracts a single JPEG frame at 5 s into the clip.
/// </summary>
public sealed class ClipTranscodingService : BackgroundService
{
    private readonly Channel<int>                        _queue;
    private readonly IServiceProvider                    _sp;
    private readonly IConfiguration                      _config;
    private readonly ILogger<ClipTranscodingService>     _log;

    public ClipTranscodingService(
        Channel<int>                     clipQueue,
        IServiceProvider                 sp,
        IConfiguration                   config,
        ILogger<ClipTranscodingService>  log)
    {
        _queue  = clipQueue;
        _sp     = sp;
        _config = config;
        _log    = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await RequeueStuckAsync(ct);

        await foreach (var clipId in _queue.Reader.ReadAllAsync(ct))
        {
            try                               { await ProcessAsync(clipId, ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)              { await MarkFailedAsync(clipId, ex.Message); }
        }
    }

    // ── Processing ────────────────────────────────────────────────────────────

    private async Task ProcessAsync(int clipId, CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StreamBGDbContext>();

        var clip = await db.Clips
            .Include(c => c.Streamer)
            .FirstOrDefaultAsync(c => c.Id == clipId, ct);

        if (clip is null || clip.Status is ClipStatus.Ready) return;

        clip.Status = ClipStatus.Processing;
        await db.SaveChangesAsync(ct);

        // ── Locate source file ────────────────────────────────────────────────
        string? sourceFile = null;

        if (clip.SourceStreamId is not null)
        {
            sourceFile = await FindLiveRecordingAsync(
                db, clip.SourceStreamId.Value, clip.OffsetSeconds, ct);
        }
        else if (clip.SourceVodId is not null)
        {
            sourceFile = await FindVodSourceAsync(db, clip.SourceVodId.Value, ct);
        }

        if (sourceFile is null || !File.Exists(sourceFile))
        {
            await MarkFailedAsync(clipId, "Файлът-източник не е намерен");
            return;
        }

        // ── Output paths ─────────────────────────────────────────────────────
        var clipsDir  = _config["Clips:OutputPath"] ?? "/data/clips";
        Directory.CreateDirectory(clipsDir);
        var mp4File  = Path.Combine(clipsDir, $"{clipId}.mp4");
        var jpgFile  = Path.Combine(clipsDir, $"{clipId}.jpg");

        _log.LogInformation("Clip {Id}: extracting {Offset}s+{Dur}s from '{Src}'",
            clipId, clip.OffsetSeconds, clip.DurationSeconds, sourceFile);

        // ── Extract clip MP4 ─────────────────────────────────────────────────
        var ok = await ExtractClipAsync(
            sourceFile, mp4File, clip.OffsetSeconds, clip.DurationSeconds, ct);

        if (!ok)
        {
            await MarkFailedAsync(clipId, "FFmpeg не успя да извлече клипа");
            return;
        }

        // ── Generate thumbnail ────────────────────────────────────────────────
        await ExtractThumbnailAsync(mp4File, jpgFile, ct);

        // ── Update DB ─────────────────────────────────────────────────────────
        var clip2 = await db.Clips.FindAsync(new object[] { clipId }, ct);
        if (clip2 is null) return;

        clip2.FilePath      = mp4File;
        clip2.ThumbnailPath = File.Exists(jpgFile) ? jpgFile : null;
        clip2.Status        = ClipStatus.Ready;
        await db.SaveChangesAsync(ct);

        _log.LogInformation("Clip {Id}: ready → {Mp4}", clipId, mp4File);
    }

    // ── Source file resolution ────────────────────────────────────────────────

    private async Task<string?> FindLiveRecordingAsync(
        StreamBGDbContext db, int streamId, double offsetSeconds, CancellationToken ct)
    {
        var stream = await db.Streams.FindAsync(new object[] { streamId }, ct);
        if (stream is null) return null;

        var recordingsPath = _config["Vod:RecordingsPath"] ?? "/data/recordings";
        if (!Directory.Exists(recordingsPath)) return null;

        // Find the most-recently-modified FLV whose name starts with the stream key
        // and whose last-write-time is after the stream's start (minus a small buffer).
        var cutoff = stream.StartedAt?.AddMinutes(-2) ?? DateTime.UtcNow.AddHours(-6);

        var file = Directory
            .GetFiles(recordingsPath, $"{stream.StreamKey}*.flv")
            .Select(f => new FileInfo(f))
            .Where(fi => fi.LastWriteTime >= cutoff)
            .OrderByDescending(fi => fi.LastWriteTime)
            .FirstOrDefault();

        return file?.FullName;
    }

    private async Task<string?> FindVodSourceAsync(
        StreamBGDbContext db, int vodId, CancellationToken ct)
    {
        var vod = await db.VodVideos.FindAsync(new object[] { vodId }, ct);
        if (vod is null) return null;

        // Prefer the raw recording file (single, seekable FLV)
        if (!string.IsNullOrEmpty(vod.RawFilePath) && File.Exists(vod.RawFilePath))
            return vod.RawFilePath;

        // Fallback: highest-quality processed HLS (also seekable by FFmpeg)
        if (!string.IsNullOrEmpty(vod.ProcessedFilePath))
        {
            var hls = Path.Combine(vod.ProcessedFilePath, "720p", "index.m3u8");
            if (File.Exists(hls)) return hls;
        }

        return null;
    }

    // ── FFmpeg helpers ────────────────────────────────────────────────────────

    private async Task<bool> ExtractClipAsync(
        string input, string output, double offsetSeconds, int duration, CancellationToken ct)
    {
        // -ss before -i  → fast seek (key-frame accurate, faster than post-input seek)
        // -t             → output duration
        // -movflags +faststart → move moov atom to front for progressive web playback
        var args = $"-loglevel warning " +
                   $"-ss {offsetSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +
                   $"-i \"{input}\" " +
                   $"-t {duration} " +
                   "-c:v libx264 -preset fast -crf 23 " +
                   "-c:a aac -b:a 128k -ar 44100 -ac 2 " +
                   "-movflags +faststart " +
                   $"\"{output}\"";

        return await RunFfmpegAsync(args, ct);
    }

    private async Task ExtractThumbnailAsync(string clipMp4, string jpgOutput, CancellationToken ct)
    {
        // Extract frame 5 seconds into the clip
        var args = $"-loglevel warning -ss 5 -i \"{clipMp4}\" " +
                   $"-frames:v 1 -q:v 2 \"{jpgOutput}\"";
        await RunFfmpegAsync(args, ct);
    }

    private async Task<bool> RunFfmpegAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("ffmpeg", args)
        {
            RedirectStandardError  = true,
            RedirectStandardOutput = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Cannot start ffmpeg");

        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var stderr = await stderrTask;

        if (!string.IsNullOrWhiteSpace(stderr))
            _log.LogDebug("ffmpeg: {Stderr}", stderr);

        return proc.ExitCode == 0;
    }

    // ── Startup re-queue & failure helpers ────────────────────────────────────

    private async Task RequeueStuckAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db    = scope.ServiceProvider.GetRequiredService<StreamBGDbContext>();
            var stuck = await db.Clips
                .Where(c => c.Status == ClipStatus.Pending || c.Status == ClipStatus.Processing)
                .Select(c => c.Id)
                .ToListAsync(ct);

            foreach (var id in stuck)
                await _queue.Writer.WriteAsync(id, ct);

            if (stuck.Count > 0)
                _log.LogInformation("Clips: re-queued {N} pending/stuck items", stuck.Count);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Could not re-queue stuck clips"); }
    }

    private async Task MarkFailedAsync(int clipId, string error)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db   = scope.ServiceProvider.GetRequiredService<StreamBGDbContext>();
            var clip = await db.Clips.FindAsync(clipId);
            if (clip is null) return;
            clip.Status = ClipStatus.Failed;
            clip.ProcessingError = error;
            await db.SaveChangesAsync();
            _log.LogError("Clip {Id} failed: {Error}", clipId, error);
        }
        catch { /* best-effort */ }
    }
}
