namespace StreamBG.Core.Entities;

// ═══════════════════════════════════════════════════════════
//  СУБСКРИПЦИИ
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Дефиниция на абонаментен план — създава се от стриймъра.
/// Напр. "Сребърен" (5 лв/мес) или "Златен" (15 лв/мес)
/// </summary>
public class SubscriptionPlan
{
    public int Id { get; set; }
    public string CreatorUserId { get; set; } = string.Empty;
    public ApplicationUser CreatorUser { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PriceMonthly { get; set; }
    public string CurrencyCode { get; set; } = "BGN";
    public string? BadgeEmoji { get; set; }
    public string? BadgeColor { get; set; }
    public List<string> Perks { get; set; } = new();

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
}

/// <summary>
/// Активна абонаментна връзка: потребител ↔ план
/// </summary>
public class UserSubscription
{
    public int Id { get; set; }
    public string SubscriberUserId { get; set; } = string.Empty;
    public ApplicationUser SubscriberUser { get; set; } = null!;

    public int PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;

    public string CreatorUserId { get; set; } = string.Empty;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public string? PaymentProvider { get; set; }
    public string? ExternalSubscriptionId { get; set; }
    public string? ExternalCustomerId { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RenewsAt { get; set; }

    public int TotalMonths { get; set; } = 1;
    public ICollection<SubscriptionPayment> Payments { get; set; } = new List<SubscriptionPayment>();
}

public enum SubscriptionStatus { Active, Cancelled, Expired, PastDue }

/// <summary>
/// Запис на всяко плащане по абонамент
/// </summary>
public class SubscriptionPayment
{
    public int Id { get; set; }
    public int UserSubscriptionId { get; set; }
    public UserSubscription UserSubscription { get; set; } = null!;

    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "BGN";
    public string? ExternalPaymentId { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
}

public enum PaymentStatus { Pending, Succeeded, Failed, Refunded }

// ═══════════════════════════════════════════════════════════
//  ДАРЕНИЯ (TIPS)
// ═══════════════════════════════════════════════════════════

public class Donation
{
    public int Id { get; set; }

    public string? DonorUserId { get; set; }
    public ApplicationUser? DonorUser { get; set; }
    public string DonorDisplayName { get; set; } = "Анонимен";

    public string RecipientUserId { get; set; } = string.Empty;
    public ApplicationUser RecipientUser { get; set; } = null!;

    public int? StreamId { get; set; }

    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "BGN";
    public string? Message { get; set; }
    public string? EmojiAnimation { get; set; }

    public string? ExternalPaymentId { get; set; }
    public string? PaymentProvider { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public bool IsAnonymous { get; set; }
    public bool IsShownInChat { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

// ═══════════════════════════════════════════════════════════
//  PUSH НОТИФИКАЦИИ
// ═══════════════════════════════════════════════════════════

public class DeviceToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string Token { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public string? DeviceModel { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}

public enum DevicePlatform { Android, iOS }

public class NotificationLog
{
    public int Id { get; set; }
    public string? UserId { get; set; }

    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ActionUrl { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();

    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum NotificationType
{
    StreamStarted,
    NewSubscriber,
    DonationReceived,
    SubscriptionRenewal,
    SubscriptionExpiring,
    SystemMessage
}

public class NotificationPreference
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public bool StreamStarted { get; set; } = true;
    public bool NewSubscriber { get; set; } = true;
    public bool DonationReceived { get; set; } = true;
    public bool SubscriptionRenewal { get; set; } = true;
    public bool EmailNotifications { get; set; } = true;
    public bool PushNotifications { get; set; } = true;
}
