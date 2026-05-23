using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using StreamBG.Core.Entities;

namespace StreamBG.Infrastructure.Data;

public static class MonetizationModelConfig
{
    private static readonly ValueComparer<List<string>> ListComparer = new(
        (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
        c => c.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
        c => c.ToList()
    );

    private static readonly ValueComparer<Dictionary<string, string>> DictComparer = new(
        (a, b) => (a == null && b == null) || (a != null && b != null && a.Count == b.Count && !a.Except(b).Any()),
        c => c.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
        c => new Dictionary<string, string>(c)
    );

    public static void Configure(ModelBuilder b)
    {
        // ── SubscriptionPlan ─────────────────────────────────────────────────
        b.Entity<SubscriptionPlan>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(60).IsRequired();
            e.Property(p => p.PriceMonthly).HasColumnType("decimal(10,2)");
            e.Property(p => p.Perks).HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new())
             .Metadata.SetValueComparer(ListComparer);
            e.HasOne(p => p.CreatorUser).WithMany()
             .HasForeignKey(p => p.CreatorUserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── UserSubscription ─────────────────────────────────────────────────
        b.Entity<UserSubscription>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.SubscriberUserId, s.PlanId });
            e.HasOne(s => s.SubscriberUser).WithMany()
             .HasForeignKey(s => s.SubscriberUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Plan).WithMany(p => p.Subscriptions)
             .HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── SubscriptionPayment ──────────────────────────────────────────────
        b.Entity<SubscriptionPayment>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Amount).HasColumnType("decimal(10,2)");
            e.HasOne(p => p.UserSubscription).WithMany(s => s.Payments)
             .HasForeignKey(p => p.UserSubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Donation ─────────────────────────────────────────────────────────
        b.Entity<Donation>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Amount).HasColumnType("decimal(10,2)");
            e.Property(d => d.Message).HasMaxLength(300);
            e.HasIndex(d => d.RecipientUserId);
            e.HasIndex(d => d.DonorUserId);
            e.HasIndex(d => d.CreatedAt);
            e.HasOne(d => d.RecipientUser).WithMany()
             .HasForeignKey(d => d.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.DonorUser).WithMany()
             .HasForeignKey(d => d.DonorUserId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── DeviceToken ──────────────────────────────────────────────────────
        b.Entity<DeviceToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.Token).IsUnique();
            e.HasIndex(t => t.UserId);
            e.HasOne(t => t.User).WithMany()
             .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── NotificationLog ──────────────────────────────────────────────────
        b.Entity<NotificationLog>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Data).HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new())
             .Metadata.SetValueComparer(DictComparer);
        });

        // ── NotificationPreference ───────────────────────────────────────────
        b.Entity<NotificationPreference>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.UserId).IsUnique();
            e.HasOne(p => p.User).WithMany()
             .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ═════════════════════════════════════════════════════════════════════
        //  SEED DATA — SubscriptionPlans
        // ═════════════════════════════════════════════════════════════════════
        b.Entity<SubscriptionPlan>().HasData(
            new SubscriptionPlan
            {
                Id = 1,
                CreatorUserId = "seed-streamer-1",
                Name = "\u041C\u0435\u0441\u0435\u0447\u0435\u043D",
                Description = "\u0411\u0430\u0437\u043E\u0432 \u0430\u0431\u043E\u043D\u0430\u043C\u0435\u043D\u0442 \u043D\u0430 \u043C\u0435\u0441\u0435\u0447\u043D\u0430 \u0431\u0430\u0437\u0430 — \u0435\u043C\u043E\u0442\u0438\u043A\u043E\u043D\u0438 \u0438 \u0440\u0435\u043A\u043B\u0430\u043C\u0438 \u043D\u0430\u043C\u0430\u043B\u0435\u043D\u0438",
                PriceMonthly = 4.99m,
                CurrencyCode = "BGN",
                BadgeEmoji = "\u2B50",
                BadgeColor = "#FFD700",
                Perks = new List<string> { "\u0427\u0430\u0442 \u0435\u043C\u043E\u0442\u0438\u043A\u043E\u043D\u0438", "\u0411\u0435\u0437 \u0440\u0435\u043A\u043B\u0430\u043C\u0438" },
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new SubscriptionPlan
            {
                Id = 2,
                CreatorUserId = "seed-streamer-1",
                Name = "\u0413\u043E\u0434\u0438\u0448\u0435\u043D",
                Description = "\u0413\u043E\u0434\u0438\u0448\u0435\u043D \u0430\u0431\u043E\u043D\u0430\u043C\u0435\u043D\u0442 \u0441 2 \u0431\u0435\u0437\u043F\u043B\u0430\u0442\u043D\u0438 \u043C\u0435\u0441\u0435\u0446\u0430 — VIP \u0431\u0435\u0439\u0434\u0436, \u0435\u043A\u0441\u043A\u043B\u0443\u0437\u0438\u0432\u0435\u043D \u0447\u0430\u0442, \u043F\u0440\u0438\u043E\u0440\u0438\u0442\u0435\u0442\u0435\u043D \u0441\u0442\u0440\u0438\u0439\u043C",
                PriceMonthly = 49.99m,
                CurrencyCode = "BGN",
                BadgeEmoji = "\uD83D\uDC51",
                BadgeColor = "#9B59B6",
                Perks = new List<string>
                {
                    "\u0427\u0430\u0442 \u0435\u043C\u043E\u0442\u0438\u043A\u043E\u043D\u0438",
                    "\u0411\u0435\u0437 \u0440\u0435\u043A\u043B\u0430\u043C\u0438",
                    "\u0415\u043A\u0441\u043A\u043B\u0443\u0437\u0438\u0432\u0435\u043D \u0447\u0430\u0442",
                    "VIP \u0431\u0435\u0439\u0434\u0436",
                    "\u041F\u0440\u0438\u043E\u0440\u0438\u0442\u0435\u0442\u0435\u043D \u0441\u0442\u0440\u0438\u0439\u043C"
                },
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // ═════════════════════════════════════════════════════════════════════
        //  SEED DATA — Donations
        // ═════════════════════════════════════════════════════════════════════
        b.Entity<Donation>().HasData(
            new Donation
            {
                Id = 1,
                DonorUserId = "seed-donor-1",
                DonorDisplayName = "testdonor",
                RecipientUserId = "seed-streamer-1",
                Amount = 10.00m,
                CurrencyCode = "BGN",
                Message = "Stratoten strim! Produlzhavai taka!",
                EmojiAnimation = "\uD83C\uDF89",
                IsAnonymous = false,
                IsShownInChat = true,
                Status = PaymentStatus.Succeeded,
                CreatedAt = new DateTime(2026, 6, 1, 18, 30, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 6, 1, 18, 30, 5, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 2,
                DonorUserId = null,
                DonorDisplayName = "Anonimen fen",
                RecipientUserId = "seed-streamer-1",
                Amount = 5.50m,
                CurrencyCode = "BGN",
                Message = "\u2764\uFE0F",
                IsAnonymous = true,
                IsShownInChat = true,
                Status = PaymentStatus.Succeeded,
                CreatedAt = new DateTime(2026, 6, 2, 20, 15, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 6, 2, 20, 15, 3, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 3,
                DonorUserId = "seed-donor-1",
                DonorDisplayName = "testdonor",
                RecipientUserId = "seed-streamer-1",
                Amount = 25.00m,
                CurrencyCode = "BGN",
                Message = "Za strima na godinata!",
                EmojiAnimation = "\uD83C\uDFC6",
                IsAnonymous = false,
                IsShownInChat = true,
                Status = PaymentStatus.Succeeded,
                CreatedAt = new DateTime(2026, 6, 5, 19, 0, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 6, 5, 19, 0, 4, DateTimeKind.Utc)
            }
        );
    }
}
