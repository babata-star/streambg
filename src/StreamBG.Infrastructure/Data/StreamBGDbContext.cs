using Microsoft.EntityFrameworkCore;
using StreamBG.Core.Entities;

namespace StreamBG.Infrastructure.Data;

public class StreamBGDbContext : DbContext
{
    public StreamBGDbContext(DbContextOptions<StreamBGDbContext> options) : base(options) { }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Core.Entities.Stream> Streams => Set<Core.Entities.Stream>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<StreamAnalytics> StreamAnalytics => Set<StreamAnalytics>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<VodVideo> VodVideos => Set<VodVideo>();
    public DbSet<VodChapter> VodChapters => Set<VodChapter>();
    public DbSet<VodComment> VodComments => Set<VodComment>();
    public DbSet<WatchProgress> WatchProgress => Set<WatchProgress>();
    public DbSet<Clip>                  Clips                  => Set<Clip>();
    public DbSet<SubscriptionPlan>       SubscriptionPlans      => Set<SubscriptionPlan>();
    public DbSet<UserSubscription>       UserSubscriptions      => Set<UserSubscription>();
    public DbSet<SubscriptionPayment>    SubscriptionPayments   => Set<SubscriptionPayment>();
    public DbSet<Donation>               Donations              => Set<Donation>();
    public DbSet<DeviceToken>            DeviceTokens           => Set<DeviceToken>();
    public DbSet<NotificationLog>        NotificationLogs       => Set<NotificationLog>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Username).HasMaxLength(32).IsRequired();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
        });

        // Seed test users for subscription & donation demo data
        builder.Entity<ApplicationUser>().HasData(
            new ApplicationUser
            {
                Id = "seed-streamer-1",
                Username = "teststreamer",
                Email = "streamer@example.com",
                PasswordHash = "SEED_ACCOUNT_NOT_FOR_LOGIN",
                Bio = "📺 Тестов стриймър акаунт за демонстрация",
                IsStreamer = true,
                IsAdmin = false,
                IsBanned = false,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ApplicationUser
            {
                Id = "seed-donor-1",
                Username = "testdonor",
                Email = "donor@example.com",
                PasswordHash = "SEED_ACCOUNT_NOT_FOR_LOGIN",
                Bio = "💰 Тестов донор акаунт за демонстрация",
                IsStreamer = false,
                IsAdmin = false,
                IsBanned = false,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        builder.Entity<Core.Entities.Stream>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.StreamKey).IsUnique();
            e.Property(s => s.Title).HasMaxLength(140).IsRequired();
            e.HasOne(s => s.User)
             .WithMany(u => u.Streams)
             .HasForeignKey(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.CategoryRef)
             .WithMany(c => c.Streams)
             .HasForeignKey(s => s.CategoryId)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
        });

        builder.Entity<ChatMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Content).HasMaxLength(500).IsRequired();
            e.HasOne(m => m.Stream)
             .WithMany(s => s.ChatMessages)
             .HasForeignKey(m => m.StreamId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Follow>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasIndex(f => new { f.FollowerId, f.FolloweeId }).IsUnique();
            e.HasOne(f => f.Follower)
             .WithMany(u => u.Following)
             .HasForeignKey(f => f.FollowerId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Followee)
             .WithMany(u => u.Followers)
             .HasForeignKey(f => f.FolloweeId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        VodModelConfig.ConfigureVod(builder);
        CategoryDbConfig.Configure(builder);
        MonetizationModelConfig.Configure(builder);

        builder.Entity<Clip>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Title).HasMaxLength(140).IsRequired();
            // Two separate FKs to ApplicationUser → must use Restrict to avoid
            // multiple cascade paths in SQL Server.
            e.HasOne(c => c.Creator)
             .WithMany()
             .HasForeignKey(c => c.CreatorUserId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Streamer)
             .WithMany()
             .HasForeignKey(c => c.StreamerUserId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => c.StreamerUserId);
            e.HasIndex(c => c.CreatedAt);
        });
    }
}
