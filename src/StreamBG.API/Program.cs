using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StreamBG.API.Hubs;
using StreamBG.API.Services;
using StreamBG.Infrastructure.Data;
using Microsoft.Extensions.Options;
using StreamBG.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<StreamBGDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Redis ─────────────────────────────────────────────────────────────────────
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "StreamBG:";
});

// ── Authentication / JWT ──────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        // Allow JWT via query string for SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireClaim("role", "admin"));
    options.AddPolicy("StreamerOnly", p => p.RequireClaim("role", "streamer", "admin"));
});

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("StreamBGPolicy", policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:3000", "http://localhost:5173" })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 32 * 1024; // 32 KB
});

// ── CDN ───────────────────────────────────────────────────────────────────────
builder.Services.Configure<CdnOptions>(builder.Configuration.GetSection(CdnOptions.Section));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ICdnService>(sp =>
{
    var cfg     = sp.GetRequiredService<IConfiguration>();
    var opts    = sp.GetRequiredService<IOptions<CdnOptions>>();
    var http    = sp.GetRequiredService<IHttpClientFactory>();
    var log     = sp.GetRequiredService<ILogger<CdnService>>();
    var hlsBase = cfg["MediaServer:HlsBaseUrl"] ?? "http://localhost:8080/hls";
    var vodBase = cfg["Vod:PublicBaseUrl"]       ?? "http://localhost:8080/vod";

    return opts.Value.Enabled
        ? new CdnService(opts, http, log, hlsBase, vodBase)
        : new NullCdnService(hlsBase, vodBase);
});

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStreamService, StreamService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<IViewerCountService, ViewerCountService>();
builder.Services.AddHostedService<SocialRelayService>();   // Background relay to FB/YT/TikTok
builder.Services.AddHostedService<StreamCleanupService>(); // Cleanup dead streams

// ── Notifications ────────────────────────────────────────────────────────────
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

// ── VOD transcoding queue ─────────────────────────────────────────────────────
// Captured at startup; VodTranscodingService receives it via constructor injection
// since it is the only Channel<int> registered in DI.
var vodChannel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = true });
builder.Services.AddSingleton(vodChannel);
builder.Services.AddHostedService<VodTranscodingService>();

// ── Clip transcoding queue ────────────────────────────────────────────────────
// Clips use their own separate channel captured in the factory lambdas below so
// they never conflict with the VOD channel in the DI container.
var clipChannel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = true });
builder.Services.AddScoped<IClipService>(sp =>
    new ClipService(
        sp.GetRequiredService<StreamBGDbContext>(),
        sp.GetRequiredService<ICdnService>(),
        clipChannel));
builder.Services.AddHostedService(sp =>
    new ClipTranscodingService(
        clipChannel,
        sp,
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<ILogger<ClipTranscodingService>>()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "StreamBG API",
        Version = "v1",
        Description = "Стрийминг платформа API"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "StreamBG v1"));
    // Auto-migrate in dev
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<StreamBGDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("StreamBGPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SignalR Hubs
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<StreamHub>("/hubs/stream");
app.MapHub<AdminHub>("/hubs/admin");

// RTMP callback endpoint (called by nginx-rtmp on stream connect/disconnect)
app.MapPost("/rtmp/on-publish", async (HttpContext ctx, IStreamService svc) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var streamKey = form["name"].ToString();
    var result = await svc.OnStreamPublishAsync(streamKey, ctx.Connection.RemoteIpAddress?.ToString());
    return result ? Results.Ok() : Results.Forbid();
});

app.MapPost("/rtmp/on-publish-done", async (HttpContext ctx, IStreamService svc) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var streamKey = form["name"].ToString();
    await svc.OnStreamEndAsync(streamKey);
    return Results.Ok();
});

// Called by nginx-rtmp when recording is complete; triggers VOD transcoding.
app.MapPost("/rtmp/on-record-done", async (
    HttpContext ctx,
    IStreamService svc,
    Channel<int> vodQueue) =>
{
    var form      = await ctx.Request.ReadFormAsync();
    var streamKey = form["name"].ToString();
    var filePath  = form["path"].ToString();

    if (string.IsNullOrEmpty(streamKey) || string.IsNullOrEmpty(filePath))
        return Results.BadRequest(new { error = "Missing name or path" });

    var vodId = await svc.CreateVodFromRecordingAsync(streamKey, filePath);
    if (vodId > 0)
        await vodQueue.Writer.WriteAsync(vodId);

    return Results.Ok();
});

app.Run();
