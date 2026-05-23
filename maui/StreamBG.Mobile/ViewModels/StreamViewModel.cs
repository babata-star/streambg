using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using LibVLCSharp.Shared;
using StreamBG.Mobile.Models;
using StreamBG.Mobile.Services;

namespace StreamBG.Mobile.ViewModels;

[QueryProperty(nameof(Stream), "stream")]
public partial class StreamViewModel : ObservableObject, IDisposable
{
    private readonly IChatService _chat;
    private readonly LibVLC _vlc;
    private Media? _media;

    [ObservableProperty] LiveStream? stream;
    [ObservableProperty] string chatInput = string.Empty;
    [ObservableProperty] int viewerCount;
    [ObservableProperty] bool isChatVisible = true;

    public MediaPlayer MediaPlayer { get; }
    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public string? AccessToken => Preferences.Get("access_token", null);
    public bool IsLoggedIn => AccessToken is not null;

    public StreamViewModel(IChatService chat)
    {
        _chat = chat;
        _vlc = new LibVLC();
        MediaPlayer = new MediaPlayer(_vlc);
        _chat.MessageReceived += OnMessage;
        _chat.MessageDeleted += OnMessageDeleted;
        _chat.ViewerCountUpdated += count => ViewerCount = count;
    }

    async partial void OnStreamChanged(LiveStream? value)
    {
        if (value is null) return;

        _media?.Dispose();
        _media = new Media(_vlc, new Uri(value.HlsUrl));
        MediaPlayer.Play(_media);

        await _chat.ConnectAsync(value.Id.ToString(), AccessToken);
        ViewerCount = value.ViewerCount;
    }

    [RelayCommand]
    async Task SendMessageAsync()
    {
        var text = ChatInput.Trim();
        if (string.IsNullOrEmpty(text) || Stream is null) return;
        ChatInput = string.Empty;
        await _chat.SendMessageAsync(Stream.Id.ToString(), text);
    }

    [RelayCommand]
    void ToggleChat() => IsChatVisible = !IsChatVisible;

    [RelayCommand]
    async Task GoBackAsync()
    {
        if (Stream is not null)
            await _chat.DisconnectAsync(Stream.Id.ToString());
        await Shell.Current.GoToAsync("..");
    }

    private void OnMessage(ChatMessage msg)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Messages.Add(msg);
            // Пази само последните 200 съобщения
            if (Messages.Count > 200)
                Messages.RemoveAt(0);
        });
    }

    private void OnMessageDeleted(int id)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var msg = Messages.FirstOrDefault(m => m.Id == id);
            if (msg is not null) Messages.Remove(msg);
        });
    }

    public void Dispose()
    {
        _chat.MessageReceived -= OnMessage;
        _chat.MessageDeleted -= OnMessageDeleted;
        MediaPlayer.Stop();
        _media?.Dispose();
        MediaPlayer.Dispose();
        _vlc.Dispose();
    }
}
