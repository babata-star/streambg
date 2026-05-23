using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using StreamBG.Mobile.Models;
using StreamBG.Mobile.Services;

namespace StreamBG.Mobile.ViewModels;

[QueryProperty(nameof(Vod), "vod")]
public partial class VodViewModel : ObservableObject
{
    private readonly IApiService _api;
    private readonly LibVLC _vlc;
    private Media? _media;
    private System.Timers.Timer? _progressTimer;

    [ObservableProperty] VodVideo? vod;
    [ObservableProperty] double currentPosition;
    [ObservableProperty] double resumePosition;
    [ObservableProperty] bool isLoading = true;

    public MediaPlayer MediaPlayer { get; }
    public string? AccessToken => Preferences.Get("access_token", null);

    public VodViewModel(IApiService api)
    {
        _api = api;
        _vlc = new LibVLC();
        MediaPlayer = new MediaPlayer(_vlc);
    }

    async partial void OnVodChanged(VodVideo? value)
    {
        if (value is null) return;
        IsLoading = true;

        // Зареди прогреса на гледане
        if (AccessToken is not null)
        {
            ResumePosition = await _api.GetWatchProgressAsync(value.Id);
        }

        IsLoading = false;

        _media?.Dispose();
        _media = new Media(_vlc, new Uri(value.HlsUrl));
        if (ResumePosition > 0)
            MediaPlayer.Time = (long)(ResumePosition * 1000);
        MediaPlayer.Play(_media);

        StartProgressTracking();
    }

    // Записва позицията на всеки 15 секунди
    private void StartProgressTracking()
    {
        _progressTimer?.Dispose();
        _progressTimer = new System.Timers.Timer(15_000);
        _progressTimer.Elapsed += async (_, _) => await SaveProgressAsync();
        _progressTimer.AutoReset = true;
        _progressTimer.Start();
    }

    public void UpdatePosition(double seconds) => CurrentPosition = seconds;

    private async Task SaveProgressAsync()
    {
        if (Vod is null || AccessToken is null) return;
        await _api.SaveWatchProgressAsync(Vod.Id, CurrentPosition);
    }

    [RelayCommand]
    async Task GoBackAsync()
    {
        _progressTimer?.Stop();
        await SaveProgressAsync();
        await Shell.Current.GoToAsync("..");
    }

    public void Dispose()
    {
        _progressTimer?.Dispose();
        MediaPlayer.Stop();
        _media?.Dispose();
        MediaPlayer.Dispose();
        _vlc.Dispose();
    }
}
