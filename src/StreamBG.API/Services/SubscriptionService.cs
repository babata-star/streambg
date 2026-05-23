using Microsoft.EntityFrameworkCore;
using StreamBG.Core.Entities;
using StreamBG.Infrastructure.Data;

namespace StreamBG.API.Services;

public interface ISubscriptionService
{
    Task<List<SubscriptionPlanDto>> GetCreatorPlansAsync(string creatorUserId);
    Task<SubscriptionPlanDto> CreatePlanAsync(string creatorUserId, CreatePlanRequest req);
    Task<SubscriptionPlanDto> UpdatePlanAsync(int planId, string creatorUserId, CreatePlanRequest req);
    Task DeletePlanAsync(int planId, string creatorUserId);

    Task<CheckoutResult> StartSubscriptionAsync(string subscriberUserId, int planId);
    Task<bool> HandleStripeWebhookAsync(string payload, string signature);
    Task CancelSubscriptionAsync(string userId, int subscriptionId);

    Task<List<UserSubscriptionDto>> GetMySubscriptionsAsync(string userId);
    Task<List<SubscriberDto>> GetMySubscribersAsync(string creatorUserId, int page);
    Task<bool> IsSubscribedAsync(string userId, string creatorUserId);
    Task<SubscriberBadge?> GetSubscriberBadgeAsync(string userId, string creatorUserId);

    Task<CreatorEarningsDto> GetEarningsAsync(string creatorUserId, DateTime from, DateTime to);
}

public class SubscriptionService : ISubscriptionService
{
    private readonly StreamBGDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IConfiguration _config;

    public SubscriptionService(StreamBGDbContext db, INotificationService notifications, IConfiguration config)
    {
        _db = db;
        _notifications = notifications;
        _config = config;
    }

    public async Task<List<SubscriptionPlanDto>> GetCreatorPlansAsync(string creatorUserId) =>
        await _db.SubscriptionPlans
            .Where(p => p.CreatorUserId == creatorUserId && p.IsActive)
            .OrderBy(p => p.PriceMonthly)
            .Select(p => ToDto(p))
            .ToListAsync();

    public async Task<SubscriptionPlanDto> CreatePlanAsync(string creatorUserId, CreatePlanRequest req)
    {
        var existing = await _db.SubscriptionPlans
            .CountAsync(p => p.CreatorUserId == creatorUserId && p.IsActive);
        if (existing >= 3)
            throw new InvalidOperationException("Максимум 3 абонаментни плана");

        var plan = new SubscriptionPlan
        {
            CreatorUserId = creatorUserId,
            Name = req.Name,
            Description = req.Description,
            PriceMonthly = req.PriceMonthly,
            BadgeEmoji = req.BadgeEmoji,
            BadgeColor = req.BadgeColor,
            Perks = req.Perks ?? new()
        };
        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync();
        return ToDto(plan);
    }

    public async Task<SubscriptionPlanDto> UpdatePlanAsync(int planId, string creatorUserId, CreatePlanRequest req)
    {
        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.CreatorUserId == creatorUserId)
            ?? throw new KeyNotFoundException("Планът не е намерен");

        plan.Name = req.Name;
        plan.Description = req.Description;
        plan.PriceMonthly = req.PriceMonthly;
        plan.BadgeEmoji = req.BadgeEmoji;
        plan.BadgeColor = req.BadgeColor;
        plan.Perks = req.Perks ?? new();
        await _db.SaveChangesAsync();
        return ToDto(plan);
    }

    public async Task DeletePlanAsync(int planId, string creatorUserId)
    {
        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.CreatorUserId == creatorUserId)
            ?? throw new KeyNotFoundException();
        plan.IsActive = false;
        await _db.SaveChangesAsync();
    }

    public async Task<CheckoutResult> StartSubscriptionAsync(string subscriberUserId, int planId)
    {
        var plan = await _db.SubscriptionPlans
            .Include(p => p.CreatorUser)
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive)
            ?? throw new KeyNotFoundException("Планът не е намерен");

        var existing = await _db.UserSubscriptions
            .AnyAsync(s => s.SubscriberUserId == subscriberUserId
                        && s.PlanId == planId
                        && s.Status == SubscriptionStatus.Active);
        if (existing)
            return new CheckoutResult(false, null, "Вече си абониран за този план");

        var sub = new UserSubscription
        {
            SubscriberUserId = subscriberUserId,
            PlanId = planId,
            CreatorUserId = plan.CreatorUserId,
            Status = SubscriptionStatus.Active,
            PaymentProvider = "demo",
            StartedAt = DateTime.UtcNow,
            RenewsAt = DateTime.UtcNow.AddMonths(1),
            ExpiresAt = DateTime.UtcNow.AddMonths(1)
        };
        _db.UserSubscriptions.Add(sub);

        _db.SubscriptionPayments.Add(new SubscriptionPayment
        {
            UserSubscription = sub,
            Amount = plan.PriceMonthly,
            CurrencyCode = plan.CurrencyCode,
            Status = PaymentStatus.Succeeded,
            PaidAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var subscriber = await _db.Users.FindAsync(subscriberUserId);
        await _notifications.SendToUserAsync(plan.CreatorUserId, new PushPayload
        {
            Type = NotificationType.NewSubscriber,
            Title = "\U0001f389 Нов абонат!",
            Body = $"{subscriber?.Username} се абонира за план \"{plan.Name}\"",
            Data = new() { ["planId"] = planId.ToString(), ["subscriberId"] = subscriberUserId }
        });

        return new CheckoutResult(true, null, null);
    }

    public async Task<bool> HandleStripeWebhookAsync(string payload, string signature)
    {
        return await Task.FromResult(true);
    }

    public async Task CancelSubscriptionAsync(string userId, int subscriptionId)
    {
        var sub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.SubscriberUserId == userId)
            ?? throw new KeyNotFoundException();

        sub.Status = SubscriptionStatus.Cancelled;
        sub.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<List<UserSubscriptionDto>> GetMySubscriptionsAsync(string userId) =>
        await _db.UserSubscriptions
            .Include(s => s.Plan).ThenInclude(p => p.CreatorUser)
            .Where(s => s.SubscriberUserId == userId && s.Status == SubscriptionStatus.Active)
            .Select(s => new UserSubscriptionDto(
                s.Id, s.Plan.CreatorUser.Username, s.Plan.CreatorUser.AvatarUrl,
                s.Plan.Name, s.Plan.PriceMonthly, s.Plan.BadgeEmoji, s.Plan.BadgeColor,
                s.StartedAt, s.RenewsAt, s.TotalMonths))
            .ToListAsync();

    public async Task<List<SubscriberDto>> GetMySubscribersAsync(string creatorUserId, int page)
    {
        const int size = 30;
        return await _db.UserSubscriptions
            .Include(s => s.SubscriberUser)
            .Include(s => s.Plan)
            .Where(s => s.CreatorUserId == creatorUserId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(s => new SubscriberDto(
                s.SubscriberUserId, s.SubscriberUser.Username, s.SubscriberUser.AvatarUrl,
                s.Plan.Name, s.Plan.BadgeEmoji, s.StartedAt, s.TotalMonths))
            .ToListAsync();
    }

    public async Task<bool> IsSubscribedAsync(string userId, string creatorUserId) =>
        await _db.UserSubscriptions.AnyAsync(s =>
            s.SubscriberUserId == userId && s.CreatorUserId == creatorUserId
            && s.Status == SubscriptionStatus.Active);

    public async Task<SubscriberBadge?> GetSubscriberBadgeAsync(string userId, string creatorUserId)
    {
        var sub = await _db.UserSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s =>
                s.SubscriberUserId == userId && s.CreatorUserId == creatorUserId
                && s.Status == SubscriptionStatus.Active);
        return sub is null ? null
            : new SubscriberBadge(sub.Plan.BadgeEmoji, sub.Plan.BadgeColor, sub.TotalMonths);
    }

    public async Task<CreatorEarningsDto> GetEarningsAsync(string creatorUserId, DateTime from, DateTime to)
    {
        var payments = await _db.SubscriptionPayments
            .Include(p => p.UserSubscription)
            .Where(p => p.UserSubscription.CreatorUserId == creatorUserId
                     && p.Status == PaymentStatus.Succeeded
                     && p.PaidAt >= from && p.PaidAt <= to)
            .ToListAsync();

        var donations = await _db.Donations
            .Where(d => d.RecipientUserId == creatorUserId
                     && d.Status == PaymentStatus.Succeeded
                     && d.CompletedAt >= from && d.CompletedAt <= to)
            .ToListAsync();

        var totalSubs = payments.Sum(p => p.Amount);
        var totalDonations = donations.Sum(d => d.Amount);
        const decimal platformFee = 0.10m;

        return new CreatorEarningsDto(
            TotalGross: totalSubs + totalDonations,
            TotalNet: (totalSubs + totalDonations) * (1 - platformFee),
            SubscriptionRevenue: totalSubs,
            DonationRevenue: totalDonations,
            PlatformFeePercent: (int)(platformFee * 100),
            ActiveSubscribers: await _db.UserSubscriptions.CountAsync(
                s => s.CreatorUserId == creatorUserId && s.Status == SubscriptionStatus.Active),
            Period: $"{from:dd.MM.yyyy} \u2013 {to:dd.MM.yyyy}"
        );
    }

    private static SubscriptionPlanDto ToDto(SubscriptionPlan p) =>
        new(p.Id, p.CreatorUserId, p.Name, p.Description, p.PriceMonthly,
            p.CurrencyCode, p.BadgeEmoji, p.BadgeColor, p.Perks);
}

// ── DTOs ──────────────────────────────────────────────────────────────────────
public record SubscriptionPlanDto(int Id, string CreatorUserId, string Name, string? Description,
    decimal PriceMonthly, string CurrencyCode, string? BadgeEmoji, string? BadgeColor, List<string> Perks);

public record UserSubscriptionDto(int Id, string CreatorUsername, string? CreatorAvatar,
    string PlanName, decimal Price, string? BadgeEmoji, string? BadgeColor,
    DateTime StartedAt, DateTime? RenewsAt, int TotalMonths);

public record SubscriberDto(string UserId, string Username, string? AvatarUrl,
    string PlanName, string? BadgeEmoji, DateTime SubscribedAt, int TotalMonths);

public record SubscriberBadge(string? Emoji, string? Color, int TotalMonths);

public record CheckoutResult(bool Success, string? CheckoutUrl, string? Error);

public record CreatorEarningsDto(decimal TotalGross, decimal TotalNet,
    decimal SubscriptionRevenue, decimal DonationRevenue,
    int PlatformFeePercent, int ActiveSubscribers, string Period);

public record CreatePlanRequest(string Name, string? Description, decimal PriceMonthly,
    string? BadgeEmoji, string? BadgeColor, List<string>? Perks);
