namespace StreamBG.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("stream",      typeof(Pages.StreamPage));
        Routing.RegisterRoute("vod",         typeof(Pages.VodPage));
        Routing.RegisterRoute("clip_player", typeof(Pages.ClipPlayerPage));

        // Navigate to auth page if not logged in
        var token = Preferences.Get("access_token", (string?)null);
        if (token is null)
        {
            Dispatcher.Dispatch(async () =>
                await GoToAsync("//auth"));
        }
    }
}
