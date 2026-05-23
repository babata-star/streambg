// ═══════════════════════════════════════════════════════════
//  Добавяния в appsettings.json
// ═══════════════════════════════════════════════════════════
/*
{
  "Firebase": {
    "ProjectId": "your-firebase-project-id",
    "ServiceAccountKeyPath": "firebase-adminsdk.json"
  },
  "Email": {
    "SmtpHost": "smtp.sendgrid.net",
    "SmtpPort": "587",
    "Username": "apikey",
    "Password": "SG.YOUR_SENDGRID_API_KEY",
    "FromAddress": "noreply@streambg.bg"
  },
  "Stripe": {
    "SecretKey": "sk_live_...",
    "WebhookSecret": "whsec_..."
  },
  "Monetization": {
    "PlatformFeePercent": 10,
    "MinDonationAmount": 1.00,
    "MaxDonationAmount": 9999.00
  }
}
*/

// ═══════════════════════════════════════════════════════════
//  Добавяния в Program.cs
// ═══════════════════════════════════════════════════════════

// СЛЕД съществуващите регистрации добави:

/*
// ── Monetization Services ──────────────────────────────────
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// FCM HTTP client
builder.Services.AddHttpClient("fcm", client => {
    client.BaseAddress = new Uri("https://fcm.googleapis.com");
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

// DbSets в StreamBGDbContext.cs:
public DbSet<SubscriptionPlan>     SubscriptionPlans     => Set<SubscriptionPlan>();
public DbSet<UserSubscription>     UserSubscriptions     => Set<UserSubscription>();
public DbSet<SubscriptionPayment>  SubscriptionPayments  => Set<SubscriptionPayment>();
public DbSet<Donation>             Donations             => Set<Donation>();
public DbSet<DeviceToken>          DeviceTokens          => Set<DeviceToken>();
public DbSet<NotificationLog>      NotificationLogs      => Set<NotificationLog>();
public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

// В OnModelCreating:
MonetizationModelConfig.Configure(builder);

// ── Добавяне в StreamService.OnStreamPublishAsync ──────────
// След stream.IsLive = true; await db.SaveChangesAsync():

var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
await StreamNotificationService.NotifyFollowersAsync(
    stream.UserId, stream.User.Username, stream.Title, notificationService);
*/

// ═══════════════════════════════════════════════════════════
//  EF Core Migration команди
// ═══════════════════════════════════════════════════════════
/*
cd src/StreamBG.API

dotnet ef migrations add AddMonetizationAndNotifications \
  --project ../StreamBG.Infrastructure \
  --startup-project .

dotnet ef database update
*/

// ═══════════════════════════════════════════════════════════
//  Firebase настройка (Android)
// ═══════════════════════════════════════════════════════════
/*
1. Отвори console.firebase.google.com
2. Създай нов проект "StreamBG"
3. Добави Android app с package name: bg.streambg.mobile
4. Свали google-services.json → постави в Platforms/Android/
5. Добави в StreamBG.Mobile.csproj:
   <GoogleServicesJson Include="Platforms\Android\google-services.json" />
6. Свали Service Account JSON → постави като Firebase:ServiceAccountKeyPath
*/

// ═══════════════════════════════════════════════════════════
//  Stripe интеграция (продукция)
// ═══════════════════════════════════════════════════════════
/*
Добави NuGet пакет: Stripe.net (версия 43+)

В SubscriptionService.StartSubscriptionAsync замени demo блока с:

var options = new SessionCreateOptions {
    PaymentMethodTypes = new List<string> { "card" },
    LineItems = new List<SessionLineItemOptions> {
        new() {
            PriceData = new SessionLineItemPriceDataOptions {
                UnitAmount = (long)(plan.PriceMonthly * 100),
                Currency = "bgn",
                Recurring = new SessionLineItemPriceDataRecurringOptions { Interval = "month" },
                ProductData = new SessionLineItemPriceDataProductDataOptions { Name = plan.Name }
            },
            Quantity = 1
        }
    },
    Mode = "subscription",
    SuccessUrl = $"https://streambg.bg/subscription/success?session_id={{CHECKOUT_SESSION_ID}}",
    CancelUrl = $"https://streambg.bg/subscription/cancel",
    Metadata = new Dictionary<string, string> {
        ["subscriber_id"] = subscriberUserId,
        ["plan_id"] = planId.ToString()
    }
};

var service = new SessionService();
var session = await service.CreateAsync(options);
return new CheckoutResult(true, session.Url, null);
*/
