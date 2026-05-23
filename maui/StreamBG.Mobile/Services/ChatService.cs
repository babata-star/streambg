using Microsoft.AspNetCore.SignalR.Client;
using StreamBG.Mobile.Models;

namespace StreamBG.Mobile.Services;

public interface IChatService
{
    event Action<ChatMessage> MessageReceived;
    event Action<int> MessageDeleted;
    event Action<int> ViewerCountUpdated;

    bool IsConnected { get; }
    Task ConnectAsync(string streamId, string? accessToken);
    Task DisconnectAsync(string streamId);
    Task SendMessageAsync(string streamId, string content);
}

public class ChatService : IChatService, IAsyncDisposable
{
    private HubConnection? _hub;

    public event Action<ChatMessage>? MessageReceived;
    public event Action<int>? MessageDeleted;
    public event Action<int>? ViewerCountUpdated;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(string streamId, string? accessToken)
    {
        if (_hub is not null) await _hub.DisposeAsync();

        var baseUrl = Preferences.Get("api_base_url", "https://your-server.bg");

        _hub = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/chat", opts =>
            {
                opts.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<object>("NewMessage", (raw) =>
        {
            // Десериализираме от dynamic JSON обект
            var json = System.Text.Json.JsonSerializer.Serialize(raw);
            var msg = System.Text.Json.JsonSerializer.Deserialize<ChatMessage>(json,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            if (msg is not null) MessageReceived?.Invoke(msg);
        });

        _hub.On<int>("MessageDeleted", id => MessageDeleted?.Invoke(id));
        _hub.On<int>("ViewerCountUpdated", count => ViewerCountUpdated?.Invoke(count));

        await _hub.StartAsync();
        await _hub.InvokeAsync("JoinStream", streamId);
    }

    public async Task DisconnectAsync(string streamId)
    {
        if (_hub is null) return;
        await _hub.InvokeAsync("LeaveStream", streamId);
        await _hub.StopAsync();
    }

    public async Task SendMessageAsync(string streamId, string content)
    {
        if (_hub is null || !IsConnected) return;
        await _hub.InvokeAsync("SendMessage", streamId, content);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync();
    }
}
