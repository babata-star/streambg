using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StreamBG.API.Hubs;
using StreamBG.API.Services;
using StreamBG.Core.Entities;
using StreamBG.Infrastructure.Data;

namespace StreamBG.API.Controllers;

[ApiController]
[Route("api/donations")]
public class DonationsController : ControllerBase
{
    private readonly StreamBGDbContext _db;
    private readonly IHubContext<ChatHub> _chatHub;
    private readonly INotificationService _notifications;
    private string? UserId => User.FindFirst("sub")?.Value;

    public DonationsController(StreamBGDbContext db,
        IHubContext<ChatHub> chatHub,
        INotificationService notifications)
    {
        _db = db;
        _chatHub = chatHub;
        _notifications = notifications;
    }

    /// <summary>Изпрати дарение</summary>
    [HttpPost]
    public async Task<IActionResult> Donate([FromBody] DonateRequest req)
    {
        var recipient = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == req.RecipientUsername);
        if (recipient is null) return NotFound(new { error = "Стриймърът не е намерен" });

        if (req.Amount < 1m) return BadRequest(new { error = "Минималното дарение е 1 лв." });
        if (req.Amount > 9999m) return BadRequest(new { error = "Максималното дарение е 9999 лв." });

        var donor = UserId is not null ? await _db.Users.FindAsync(UserId) : null;

        var donation = new Donation
        {
            DonorUserId = UserId,
            DonorDisplayName = req.IsAnonymous ? "Анонимен" : (donor?.Username ?? req.DonorName ?? "Анонимен"),
            RecipientUserId = recipient.Id,
            StreamId = req.StreamId,
            Amount = req.Amount,
            CurrencyCode = "BGN",
            Message = req.Message?.Trim(),
            EmojiAnimation = req.EmojiAnimation ?? PickEmojiForAmount(req.Amount),
            IsAnonymous = req.IsAnonymous,
            PaymentProvider = "demo",
            Status = PaymentStatus.Succeeded,
            CompletedAt = DateTime.UtcNow
        };

        _db.Donations.Add(donation);
        await _db.SaveChangesAsync();

        // ── Покажи дарението в чата на стрийма ───────────────────────────────
        if (req.StreamId.HasValue && donation.IsShownInChat)
        {
            await _chatHub.Clients
                .Group($"stream:{req.StreamId}")
                .SendAsync("DonationReceived", new
                {
                    id          = donation.Id,
                    donorName   = donation.DonorDisplayName,
                    amount      = donation.Amount,
                    currency    = donation.CurrencyCode,
                    message     = donation.Message,
                    emoji       = donation.EmojiAnimation,
                    animationMs = CalculateAnimationDuration(donation.Amount)
                });
        }

        // ── Push нотификация към стриймъра ────────────────────────────────────
        await _notifications.SendToUserAsync(recipient.Id, new PushPayload
        {
            Type  = NotificationType.DonationReceived,
            Title = $"{donation.EmojiAnimation} Дарение {donation.Amount:F2} лв.!",
            Body  = string.IsNullOrEmpty(donation.Message)
                    ? $"от {donation.DonorDisplayName}"
                    : $"от {donation.DonorDisplayName}: \"{donation.Message}\"",
            Data  = new() { ["donationId"] = donation.Id.ToString() }
        });

        return Ok(new { donationId = donation.Id, message = "Дарението е изпратено!" });
    }

    /// <summary>История на получените дарения (стриймър)</summary>
    [HttpGet("received")]
    [Authorize(Policy = "StreamerOnly")]
    public async Task<IActionResult> Received([FromQuery] int page = 1)
    {
        const int size = 30;
        var userId = UserId!;
        var items = await _db.Donations
            .Where(d => d.RecipientUserId == userId && d.Status == PaymentStatus.Succeeded)
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(d => new DonationDto(d.Id, d.DonorDisplayName, d.Amount, d.CurrencyCode,
                d.Message, d.EmojiAnimation, d.CreatedAt))
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>Топ дарители на стриймъра</summary>
    [HttpGet("top/{creatorUsername}")]
    public async Task<IActionResult> TopDonors(string creatorUsername)
    {
        var creator = await _db.Users.FirstOrDefaultAsync(u => u.Username == creatorUsername);
        if (creator is null) return NotFound();

        var top = await _db.Donations
            .Where(d => d.RecipientUserId == creator.Id
                     && d.Status == PaymentStatus.Succeeded
                     && !d.IsAnonymous)
            .GroupBy(d => d.DonorDisplayName)
            .Select(g => new { donor = g.Key, total = g.Sum(d => d.Amount) })
            .OrderByDescending(x => x.total)
            .Take(10)
            .ToListAsync();

        return Ok(top);
    }

    private static string PickEmojiForAmount(decimal amount) => amount switch
    {
        >= 100 => "\U0001f451",
        >= 50  => "\U0001f48e",
        >= 20  => "\U0001f525",
        >= 10  => "\u2b50",
        >= 5   => "\U0001f49c",
        _      => "\U0001f389"
    };

    private static int CalculateAnimationDuration(decimal amount) => amount switch
    {
        >= 50 => 8000,
        >= 20 => 6000,
        >= 10 => 5000,
        _     => 4000
    };
}

public record DonateRequest(
    string RecipientUsername,
    decimal Amount,
    string? Message = null,
    string? EmojiAnimation = null,
    string? DonorName = null,
    bool IsAnonymous = false,
    int? StreamId = null
);

public record DonationDto(int Id, string DonorName, decimal Amount,
    string Currency, string? Message, string? Emoji, DateTime CreatedAt);
