using CommunityToolkit.Maui;
using LibVLCSharp.MAUI;
using StreamBG.Mobile.Pages;
using StreamBG.Mobile.Services;
using StreamBG.Mobile.ViewModels;

namespace StreamBG.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseLibVLCSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Outfit-Regular.ttf", "OutfitRegular");
                fonts.AddFont("Outfit-Bold.ttf", "OutfitBold");
                fonts.AddFont("Outfit-ExtraBold.ttf", "OutfitExtraBold");
            });

        // ── Services ───────────────────────────────────────────────────────────
        var apiBaseUrl = Preferences.Get("api_base_url", "https://your-server.bg");

        builder.Services.AddHttpClient<IApiService, ApiService>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddSingleton<IChatService, ChatService>();

        // ── Pages ──────────────────────────────────────────────────────────────
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<StreamPage>();
        builder.Services.AddTransient<VodPage>();
        builder.Services.AddTransient<AuthPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<SearchPage>();
        builder.Services.AddTransient<ClipPage>();
        builder.Services.AddTransient<ClipPlayerPage>();

        // ── ViewModels ─────────────────────────────────────────────────────────
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<StreamViewModel>();
        builder.Services.AddTransient<VodViewModel>();
        builder.Services.AddTransient<AuthViewModel>();
        builder.Services.AddTransient<SearchViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<ClipViewModel>();
        builder.Services.AddTransient<ClipPlayerViewModel>();

        return builder.Build();
    }
}
