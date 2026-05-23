using Microsoft.EntityFrameworkCore;
using StreamBG.Core.Entities;
using StreamBG.Infrastructure.Data;
using System.Text;
using System.Text.Json;

namespace StreamBG.API.Services;

public record PushPayload
{
    public NotificationType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string? ActionUrl { get; init; }
    public Dictionary<string, string> Data { get; init; } = new();
}

public interface INotificationService
{
    Task SendToUserAsync(string userId, PushPayload payload);
    Task SendToFollowersAsync(string creatorUserId, PushPayload payload);
    Task BroadcastAsync(PushPayload payload);
    Task RegisterDeviceTokenAsync(string userId, string token, DevicePlatform platform);
    Task UnregisterDeviceTokenAsync(string token);
}

/// <summary>
/// Изпраща push нотификации чрез Firebase Cloud Messaging (FCM v1 API).
/// Поддържа и Apple Push Notification Service (APNs) чрез FCM gateway.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly StreamBGDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationService> _logger;
    private readonly HttpClient _http;

    // FCM v1 endpoint
    private const string FcmUrl = "https://fcm.googleapis.com/v1/projects/{0}/messages:send";

    public NotificationService(
        StreamBGDbContext db,
        IConfiguration config,
        ILogger<NotificationService> logger,
        IHttpClientFactory httpFactory)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _http = httpFactory.CreateClient("fcm");
    }

    public async Task RegisterDeviceTokenAsync(string userId, string token, DevicePlatform platform)
    {
        var existing = await _db.DeviceTokens
            .FirstOrDefaultAsync(t => t.Token == token);

        if (existing is not null)
        {
            existing.UserId = userId;
            existing.IsActive = true;
            existing.LastUsedAt = DateTime.UtcNow;
        }
        else
        {
            _db.DeviceTokens.Add(new DeviceToken
            {
                UserId = userId,
                Token = token,
                Platform = platform,
                RegisteredAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task UnregisterDeviceTokenAsync(string token)
    {
        var t = await _db.DeviceTokens.FirstOrDefaultAsync(x => x.Token == token);
        if (t is null) return;
        t.IsActive = false;
        await _db.SaveChangesAsync();
    }

    public async Task SendToUserAsync(string userId, PushPayload payload)
    {
        var prefs = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (prefs?.PushNotifications == false) return;
        if (!ShouldSend(prefs, payload.Type)) return;

        var tokens = await _db.DeviceTokens
            .Where(t => t.UserId == userId && t.IsActive)
            .Select(t => t.Token)
            .ToListAsync();

        if (tokens.Count == 0) return;

        var failed = new List<string>();
        foreach (var token in tokens)
        {
            var ok = await SendFcmAsync(token, payload);
            if (!ok) failed.Add(token);
        }

        if (failed.Any())
        {
            await _db.DeviceTokens
                .Where(t => failed.Contains(t.Token))
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.IsActive, false));
        }

        await LogNotificationAsync(userId, payload, tokens.Count, failed.Count);
    }

    public async Task SendToFollowersAsync(string creatorUserId, PushPayload payload)
    {
        var followerIds = await _db.Follows
            .Where(f => f.FolloweeId == creatorUserId && f.NotificationsEnabled)
            .Select(f => f.FollowerId)
            .ToListAsync();

        _logger.LogInformation("Изпращане на нотификация до {Count} последователи на {CreatorId}",
            followerIds.Count, creatorUserId);

        foreach (var batch in followerIds.Chunk(500))
        {
            var tasks = batch.Select(uid => SendToUserAsync(uid, payload));
            await Task.WhenAll(tasks);
        }
    }

    public async Task BroadcastAsync(PushPayload payload)
    {
        var projectId = _config["Firebase:ProjectId"];
        if (string.IsNullOrEmpty(projectId)) return;

        var message = BuildFcmMessage(null, payload, topic: "all");
        await PostFcmAsync(projectId, message);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<bool> SendFcmAsync(string token, PushPayload payload)
    {
        var projectId = _config["Firebase:ProjectId"];
        if (string.IsNullOrEmpty(projectId))
        {
            _logger.LogWarning("Firebase:ProjectId не е конфигуриран — нотификацията е симулирана");
            _logger.LogInformation("\U0001f4f2 PUSH \u2192 {Title}: {Body}", payload.Title, payload.Body);
            return true;
        }

        var message = BuildFcmMessage(token, payload);
        return await PostFcmAsync(projectId, message);
    }

    private static object BuildFcmMessage(string? token, PushPayload payload, string? topic = null)
    {
        var data = new Dictionary<string, string>(payload.Data)
        {
            ["type"]      = payload.Type.ToString(),
            ["actionUrl"] = payload.ActionUrl ?? "",
        };

        var notification = new
        {
            title = payload.Title,
            body  = payload.Body,
            image = payload.ImageUrl
        };

        if (token is not null)
        {
            return new
            {
                message = new
                {
                    token,
                    notification,
                    data,
                    android = new { priority = "high", notification = new { sound = "default", channel_id = "streambg" } },
                    apns   = new { payload = new { aps = new { sound = "default", badge = 1 } } }
                }
            };
        }
        return new { message = new { topic, notification, data } };
    }

    private async Task<bool> PostFcmAsync(string projectId, object message)
    {
        try
        {
            var url = string.Format(FcmUrl, projectId);
            var json = JsonSerializer.Serialize(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync(url, content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                _logger.LogError("FCM грешка: {Status} \u2014 {Body}", resp.StatusCode, body);
                return resp.StatusCode != System.Net.HttpStatusCode.NotFound;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при изпращане на FCM нотификация");
            return false;
        }
    }

    private async Task LogNotificationAsync(string userId, PushPayload payload, int sent, int failed)
    {
        _db.NotificationLogs.Add(new NotificationLog
        {
            UserId     = userId,
            Type       = payload.Type,
            Title      = payload.Title,
            Body       = payload.Body,
            Data       = payload.Data,
            SentCount  = sent,
            FailedCount = failed
        });
        await _db.SaveChangesAsync();
    }

    private static bool ShouldSend(NotificationPreference? prefs, NotificationType type)
    {
        if (prefs is null) return true;
        return type switch
        {
            NotificationType.StreamStarted          => prefs.StreamStarted,
            NotificationType.NewSubscriber          => prefs.NewSubscriber,
            NotificationType.DonationReceived       => prefs.DonationReceived,
            NotificationType.SubscriptionRenewal    => prefs.SubscriptionRenewal,
            NotificationType.SubscriptionExpiring   => prefs.SubscriptionRenewal,
            NotificationType.SystemMessage          => true,
            _                                       => true
        };
    }
}

/// <summary>
/// Utility за изпращане на нотификации при стартиране на стрийм.
/// Извиква се от StreamService при OnStreamPublishAsync.
/// </summary>
public static class StreamNotificationHelper
{
    public static async Task NotifyFollowersAsync(
        string creatorUserId,
        string creatorUsername,
        string streamTitle,
        INotificationService notifications)
    {
        await notifications.SendToFollowersAsync(creatorUserId, new PushPayload
        {
            Type       = NotificationType.StreamStarted,
            Title      = $"🔴 {creatorUsername} е на живо!",
            Body       = streamTitle,
            ActionUrl  = $"streambg://stream/{creatorUsername}",
            Data       = new()
            {
                ["username"]    = creatorUsername,
                ["creatorId"]   = creatorUserId,
                ["screen"]      = "stream"
            }
        });
    }
}
