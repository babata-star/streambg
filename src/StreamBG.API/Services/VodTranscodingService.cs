using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using StreamBG.Core.Entities;
using StreamBG.Infrastructure.Data;

namespace StreamBG.API.Services;

/// <summary>
/// Background service that transcodes recorded VOD files to multi-quality HLS
/// (720p / 480p / 360p) using FFmpeg.
///
/// Work items (VodVideo IDs) are fed through the singleton <see cref="Channel{Int32}"/>
/// registered in Program.cs.  On startup, VODs stuck in Processing or Pending state
/// are automatically re-queued so they survive container restarts.
///
/// Output layout per VOD:
///   {Vod:OutputPath}/{userId}/{vodId}/master.m3u8
///   {Vod:OutputPath}/{userId}/{vodId}/720p/index.m3u8  (+ *.ts)
///   {Vod:OutputPath}/{userId}/{vodId}/480p/index.m3u8  (+ *.ts)
///   {Vod:OutputPath}/{userId}/{vodId}/360p/index.m3u8  (+ *.ts)
/// </summary>
public sealed class VodTranscodingService : BackgroundService
{
    private readonly Channel<int>                   _queue;
    private readonly IServiceProvider               _sp;
    private readonly IConfiguration                 _config;
    private readonly ILogger<VodTranscodingService> _log;

    public VodTranscodingService(
        Channel<int>                   queue,
        IServiceProvider               sp,
        IConfiguration                 config,
        ILogger<VodTranscodingService> log)
    {
        _queue  = queue;
        _sp     = sp;
        _config = config;
        _log    = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Re-enqueue anything that was in-flight when the container last stopped
        await RequeueStuckAsync(ct);

        await foreach (var vodId in _queue.Reader.ReadAllAsync(ct))
        {
            try                        { await TranscodeAsync(vodId, ct); }
            catch (OperationCanceledException)  { break; }
            catch (Exception ex) { await MarkFailedAsync(vodId, ex.Message); }
        }
    }

    // ── Transcode pipeline ────────────────────────────────────────────────────

    private async Task TranscodeAsync(int vodId, CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StreamBGDbContext>();

        var vod = await db.VodVideos.FindAsync(new object[] { vodId }, ct);
        if (vod is null || vod.Status is VodStatus.Ready or VodStatus.Deleted)
            return;

        if (string.IsNullOrEmpty(vod.RawFilePath) || !File.Exists(vod.RawFilePath))
        {
            _log.LogWarning("VOD {Id}: raw file not found at '{Path}'", vodId, vod.RawFilePath);
            vod.Status = VodStatus.Failed;
            vod.ProcessingError = "Raw recording file not found";
            await db.SaveChangesAsync(ct);
            return;
        }

        vod.Status = VodStatus.Processing;
        await db.SaveChangesAsync(ct);

        var vodBase = _config["Vod:OutputPath"] ?? "/data/vod";
        var outDir  = Path.Combine(vodBase, vod.UserId, vodId.ToString());
        Directory.CreateDirectory(Path.Combine(outDir, "720p"));
        Directory.CreateDirectory(Path.Combine(outDir, "480p"));
        Directory.CreateDirectory(Path.Combine(outDir, "360p"));

        _log.LogInformation("VOD {Id}: starting transcode → {OutDir}", vodId, outDir);

        var success = await RunFfmpegAsync(vod.RawFilePath, outDir, ct);
        if (!success)
        {
            await MarkFailedAsync(vodId, "FFmpeg exited with non-zero exit code");
            return;
        }

        // Write the ABR master playlist
        const string masterPlaylist =
            "#EXTM3U\n" +
            "#EXT-X-VERSION:3\n" +
            "#EXT-X-STREAM-INF:BANDWIDTH=3000000,RESOLUTION=1280x720,CODECS=\"avc1.4d401f,mp4a.40.2\",NAME=\"720p\"\n" +
            "720p/index.m3u8\n" +
            "#EXT-X-STREAM-INF:BANDWIDTH=1500000,RESOLUTION=854x480,CODECS=\"avc1.42e01e,mp4a.40.2\",NAME=\"480p\"\n" +
            "480p/index.m3u8\n" +
            "#EXT-X-STREAM-INF:BANDWIDTH=800000,RESOLUTION=640x360,CODECS=\"avc1.42e01e,mp4a.40.2\",NAME=\"360p\"\n" +
            "360p/index.m3u8\n";

        await File.WriteAllTextAsync(Path.Combine(outDir, "master.m3u8"), masterPlaylist, ct);

        // Probe duration from the source recording
        var duration = await GetDurationAsync(vod.RawFilePath, ct);
        var fileSize = new FileInfo(vod.RawFilePath).Length;

        // Re-fetch to avoid EF concurrency conflicts
        var vod2 = await db.VodVideos.FindAsync(new object[] { vodId }, ct);
        if (vod2 is null) return;

        vod2.Status            = VodStatus.Ready;
        vod2.ProcessedFilePath = outDir;
        vod2.ProcessedAt       = DateTime.UtcNow;
        vod2.Duration          = duration;
        vod2.FileSizeBytes     = fileSize;
        await db.SaveChangesAsync(ct);

        _log.LogInformation("VOD {Id}: ready — duration={Duration}", vodId, duration);
    }

    // ── FFmpeg invocation ─────────────────────────────────────────────────────

    private async Task<bool> RunFfmpegAsync(string input, string outDir, CancellationToken ct)
    {
        // filter_complex: split video into three tracks and scale each to target resolution,
        // padding with black bars to preserve aspect ratio.
        const string filter =
            "[0:v]split=3[v720][v480][v360];" +
            "[v720]scale=w=1280:h=720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2:black[s720];" +
            "[v480]scale=w=854:h=480:force_original_aspect_ratio=decrease,pad=854:480:(ow-iw)/2:(oh-ih)/2:black[s480];" +
            "[v360]scale=w=640:h=360:force_original_aspect_ratio=decrease,pad=640:360:(ow-iw)/2:(oh-ih)/2:black[s360]";

        // hls_list_size 0  — retain all segments in the VOD playlist (unlike live which uses 5)
        // hls_time 4       — 4-second segments reduce HTTP overhead vs 2-second live segments
        // preset medium    — better quality/size tradeoff for offline transcoding
        var sb = new StringBuilder();
        sb.Append($"-loglevel warning -i \"{input}\" ");
        sb.Append($"-filter_complex \"{filter}\" ");

        AppendQuality(sb, "s720", "2800k", "3000k", "6000k", "128k", $"{outDir}/720p");
        AppendQuality(sb, "s480", "1400k", "1500k", "3000k", "96k",  $"{outDir}/480p");
        AppendQuality(sb, "s360", "700k",  "800k",  "1600k", "64k",  $"{outDir}/360p");

        var psi = new ProcessStartInfo("ffmpeg", sb.ToString())
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start ffmpeg process");

        // Drain stderr in a background task to prevent the process from blocking
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);

        await proc.WaitForExitAsync(ct);
        var stderr = await stderrTask;

        if (!string.IsNullOrWhiteSpace(stderr))
            _log.LogDebug("FFmpeg VOD [{OutDir}]: {Stderr}", outDir, stderr);

        if (proc.ExitCode != 0)
            _log.LogError("FFmpeg VOD [{OutDir}] exited {Code}. Stderr: {Err}",
                outDir, proc.ExitCode, stderr);

        return proc.ExitCode == 0;
    }

    private static void AppendQuality(
        StringBuilder sb, string videoMap,
        string bitrate, string maxRate, string bufSize, string audioBitrate,
        string outDir)
    {
        sb.Append($"-map \"[{videoMap}]\" -map \"0:a?\" ");
        sb.Append($"-c:v libx264 -preset medium -crf 22 ");
        sb.Append($"-b:v {bitrate} -maxrate {maxRate} -bufsize {bufSize} ");
        sb.Append($"-g 96 -keyint_min 96 -sc_threshold 0 ");
        sb.Append($"-c:a aac -b:a {audioBitrate} -ar 44100 -ac 2 ");
        sb.Append($"-f hls -hls_time 4 -hls_list_size 0 -hls_flags independent_segments ");
        sb.Append($"-hls_segment_type mpegts ");
        sb.Append($"-hls_segment_filename \"{outDir}/%06d.ts\" ");
        sb.Append($"\"{outDir}/index.m3u8\" ");
    }

    // ── ffprobe duration ──────────────────────────────────────────────────────

    private async Task<TimeSpan?> GetDurationAsync(string filePath, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo(
                "ffprobe",
                $"-v error -show_entries format=duration -of csv=p=0 \"{filePath}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            using var proc = Process.Start(psi) ?? throw new Exception("ffprobe start failed");
            var output = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            if (double.TryParse(output.Trim(), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var secs))
                return TimeSpan.FromSeconds(secs);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "ffprobe failed for '{Path}'", filePath);
        }

        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task MarkFailedAsync(int vodId, string error)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StreamBGDbContext>();
            var vod = await db.VodVideos.FindAsync(vodId);
            if (vod is null) return;
            vod.Status = VodStatus.Failed;
            vod.ProcessingError = error;
            await db.SaveChangesAsync();
            _log.LogError("VOD {Id} transcoding failed: {Error}", vodId, error);
        }
        catch { /* best-effort */ }
    }

    private async Task RequeueStuckAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StreamBGDbContext>();

            var stuckIds = await db.VodVideos
                .Where(v => v.Status == VodStatus.Pending || v.Status == VodStatus.Processing)
                .Select(v => v.Id)
                .ToListAsync(ct);

            foreach (var id in stuckIds)
                await _queue.Writer.WriteAsync(id, ct);

            if (stuckIds.Count > 0)
                _log.LogInformation("VOD: re-queued {Count} pending/stuck items on startup", stuckIds.Count);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not re-queue stuck VODs on startup");
        }
    }
}
