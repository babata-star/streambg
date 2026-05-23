using System.Collections.Concurrent;
using System.Diagnostics;
using StreamBG.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace StreamBG.API.Services;

/// <summary>
/// Бекграунд сервиз, който препраща живи стриймове към Facebook Live,
/// YouTube Live и TikTok едновременно чрез FFmpeg.
/// </summary>
public class SocialRelayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<SocialRelayService> _logger;

    // streamId -> FFmpeg процес
    private readonly ConcurrentDictionary<int, Process> _activeRelays = new();

    public SocialRelayService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<SocialRelayService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Social Relay Service стартира...");

        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncRelaysAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task SyncRelaysAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StreamBGDbContext>();

        // Намери всички живи стриймове с активирано социално препращане
        var liveStreams = await db.Streams
            .Where(s => s.IsLive && (s.RelayToYouTube || s.RelayToFacebook || s.RelayToTikTok))
            .ToListAsync(ct);

        var liveIds = liveStreams.Select(s => s.Id).ToHashSet();

        // Спри релейтата за приключили стриймове
        foreach (var (id, proc) in _activeRelays)
        {
            if (!liveIds.Contains(id))
            {
                StopRelay(id);
            }
        }

        // Стартирай нови релейта
        foreach (var stream in liveStreams)
        {
            if (!_activeRelays.ContainsKey(stream.Id))
            {
                StartRelay(stream);
            }
        }
    }

    private void StartRelay(Core.Entities.Stream stream)
    {
        var inputRtmp = $"{_config["MediaServer:RtmpUrl"]}/{stream.StreamKey}";
        var outputs = new List<string>();

        if (stream.RelayToYouTube && !string.IsNullOrEmpty(stream.YouTubeStreamKey))
            outputs.Add($"-c copy -f flv {_config["SocialRelay:YouTubeRtmpUrl"]}/{stream.YouTubeStreamKey}");

        if (stream.RelayToFacebook && !string.IsNullOrEmpty(stream.FacebookStreamKey))
            outputs.Add($"-c copy -f flv {_config["SocialRelay:FacebookRtmpUrl"]}/{stream.FacebookStreamKey}");

        if (stream.RelayToTikTok && !string.IsNullOrEmpty(stream.TikTokStreamKey))
            outputs.Add($"-c copy -f flv {_config["SocialRelay:TikTokRtmpUrl"]}/{stream.TikTokStreamKey}");

        if (outputs.Count == 0) return;

        // Изгради FFmpeg командата
        // -re = четене в реалтайм темп
        // -i = входен поток
        // Множество изходни дестинации
        var ffmpegArgs = $"-re -i \"{inputRtmp}\" {string.Join(" ", outputs)}";

        _logger.LogInformation("Стартиране на social relay за стрийм {StreamId}: ffmpeg {Args}",
            stream.Id, ffmpegArgs);

        var psi = new ProcessStartInfo("ffmpeg", ffmpegArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _logger.LogDebug("FFmpeg [{StreamId}]: {Data}", stream.Id, e.Data);
        };

        process.Exited += (_, _) =>
        {
            _logger.LogInformation("Social relay за стрийм {StreamId} приключи.", stream.Id);
            _activeRelays.TryRemove(stream.Id, out _);
        };

        process.Start();
        process.BeginErrorReadLine();

        _activeRelays[stream.Id] = process;
    }

    private void StopRelay(int streamId)
    {
        if (_activeRelays.TryRemove(streamId, out var proc))
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill();
                    _logger.LogInformation("Спрян social relay за стрийм {StreamId}", streamId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Грешка при спиране на relay за {StreamId}", streamId);
            }
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        // Спри всички активни FFmpeg процеси
        foreach (var id in _activeRelays.Keys.ToList())
            StopRelay(id);

        await base.StopAsync(ct);
    }
}

/// <summary>
/// Почиства стриймове, при които nginx не е изпратил on-publish-done
/// (crash, загубена връзка и т.н.)
/// </summary>
public class StreamCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StreamCleanupService> _logger;

    public StreamCleanupService(IServiceScopeFactory scopeFactory, ILogger<StreamCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await CleanupStaleStreamsAsync();
            await Task.Delay(TimeSpan.FromMinutes(2), ct);
        }
    }

    private async Task CleanupStaleStreamsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StreamBGDbContext>();

        // Стриймове, стартирали преди >5 ч без да са приключили — вероятно са "замръзнали"
        var threshold = DateTime.UtcNow.AddHours(-5);
        var stale = await db.Streams
            .Where(s => s.IsLive && s.StartedAt < threshold)
            .ToListAsync();

        foreach (var s in stale)
        {
            s.IsLive = false;
            s.EndedAt = DateTime.UtcNow;
            _logger.LogWarning("Автоматично приключен застарял стрийм {StreamId}", s.Id);
        }

        if (stale.Any())
            await db.SaveChangesAsync();
    }
}
