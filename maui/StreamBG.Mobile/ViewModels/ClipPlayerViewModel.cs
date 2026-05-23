using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using StreamBG.Mobile.Models;
using StreamBG.Mobile.Services;

namespace StreamBG.Mobile.ViewModels;

[QueryProperty(nameof(Clip), "clip")]
public partial class ClipPlayerViewModel : ObservableObject, IDisposable
{
    private readonly IApiService _api;
    private readonly LibVLC _vlc;
    private Media? _media;
    private bool _viewRecorded;

    [ObservableProperty] Clip? clip;
    [ObservableProperty] bool isLoading = true;

    public MediaPlayer MediaPlayer { get; }

    public ClipPlayerViewModel(IApiService api)
    {
        _api = api;
        _vlc = new LibVLC();
        MediaPlayer = new MediaPlayer(_vlc);
        MediaPlayer.Playing += OnPlaying;
    }

    // ── QueryProperty callback ────────────────────────────────────────────────

    async partial void OnClipChanged(Clip? value)
    {
        if (value?.VideoUrl is null) return;
        IsLoading = true;

        // Fire-and-forget view count
        if (!_viewRecorded)
        {
            _viewRecorded = true;
            _ = _api.RecordClipViewAsync(value.Id);
        }

        _media?.Dispose();
        _media = new Media(_vlc, new Uri(value.VideoUrl));
        MediaPlayer.Play(_media);
    }

    private void OnPlaying(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    [RelayCommand]
    async Task GoBackAsync()
    {
        MediaPlayer.Stop();
        await Shell.Current.GoToAsync("..");
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        MediaPlayer.Playing -= OnPlaying;
        MediaPlayer.Stop();
        _media?.Dispose();
        MediaPlayer.Dispose();
        _vlc.Dispose();
    }
}
