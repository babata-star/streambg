using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StreamBG.API.Services;
using StreamBG.Core.Entities;
using StreamBG.Infrastructure.Data;

namespace StreamBG.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    private readonly StreamBGDbContext _db;
    private string UserId => User.FindFirst("sub")?.Value ?? string.Empty;

    public NotificationsController(INotificationService notifications, StreamBGDbContext db)
    {
        _notifications = notifications;
        _db = db;
    }

    /// <summary>Регистрирай device token (при стартиране на app)</summary>
    [HttpPost("device")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest req)
    {
        await _notifications.RegisterDeviceTokenAsync(UserId, req.Token, req.Platform);
        return Ok(new { message = "Device регистриран успешно" });
    }

    /// <summary>Премахни device token (при logout)</summary>
    [HttpDelete("device")]
    public async Task<IActionResult> UnregisterDevice([FromBody] UnregisterDeviceRequest req)
    {
        await _notifications.UnregisterDeviceTokenAsync(req.Token);
        return Ok();
    }

    /// <summary>Вземи предпочитания за нотификации</summary>
    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        var prefs = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == UserId);

        return Ok(prefs ?? new NotificationPreference
        {
            UserId           = UserId,
            StreamStarted    = true,
            NewSubscriber    = true,
            DonationReceived = true,
            SubscriptionRenewal = true,
            EmailNotifications = true,
            PushNotifications  = true
        });
    }

    /// <summary>Обнови предпочитания</summary>
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest req)
    {
        var prefs = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == UserId);

        if (prefs is null)
        {
            prefs = new NotificationPreference { UserId = UserId };
            _db.NotificationPreferences.Add(prefs);
        }

        prefs.StreamStarted       = req.StreamStarted;
        prefs.NewSubscriber       = req.NewSubscriber;
        prefs.DonationReceived    = req.DonationReceived;
        prefs.SubscriptionRenewal = req.SubscriptionRenewal;
        prefs.EmailNotifications  = req.EmailNotifications;
        prefs.PushNotifications   = req.PushNotifications;

        await _db.SaveChangesAsync();
        return Ok(prefs);
    }

    /// <summary>Тест нотификация (само за разработка)</summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test()
    {
        await _notifications.SendToUserAsync(UserId, new PushPayload
        {
            Type  = NotificationType.SystemMessage,
            Title = "\U0001f389 Тест нотификация!",
            Body  = "Push нотификациите работят правилно.",
            Data  = new() { ["test"] = "true" }
        });
        return Ok(new { message = "Тест нотификацията е изпратена" });
    }

    /// <summary>Admin: изпрати broadcast до всички</summary>
    [HttpPost("broadcast")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest req)
    {
        await _notifications.BroadcastAsync(new PushPayload
        {
            Type  = NotificationType.SystemMessage,
            Title = req.Title,
            Body  = req.Body
        });
        return Ok(new { message = "Broadcast изпратен" });
    }
}

public record RegisterDeviceRequest(string Token, DevicePlatform Platform);
public record UnregisterDeviceRequest(string Token);
public record UpdatePreferencesRequest(
    bool StreamStarted, bool NewSubscriber, bool DonationReceived,
    bool SubscriptionRenewal, bool EmailNotifications, bool PushNotifications);
public record BroadcastRequest(string Title, string Body);
