using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StreamBG.API.Services;

namespace StreamBG.API.Hubs;

/// <summary>
/// Реалтаймов чат хъб — подобен на Twitch чата.
/// Всеки стрийм е отделна SignalR група.
/// </summary>
public class ChatHub : Hub
{
    private readonly IChatService _chat;
    private readonly IViewerCountService _viewers;

    public ChatHub(IChatService chat, IViewerCountService viewers)
    {
        _chat = chat;
        _viewers = viewers;
    }

    /// <summary>Присъединяване към стрийм чат</summary>
    public async Task JoinStream(string streamId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"stream:{streamId}");
        await _viewers.IncrementAsync(streamId);
        _viewers.TrackConnection(Context.ConnectionId, streamId);

        var count = await _viewers.GetCountAsync(streamId);
        await Clients.Group($"stream:{streamId}")
            .SendAsync("ViewerCountUpdated", count);
    }

    /// <summary>Напускане на стрийм чат</summary>
    public async Task LeaveStream(string streamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"stream:{streamId}");
        await _viewers.DecrementAsync(streamId);

        var count = await _viewers.GetCountAsync(streamId);
        await Clients.Group($"stream:{streamId}")
            .SendAsync("ViewerCountUpdated", count);
    }

    /// <summary>Изпращане на съобщение в чата</summary>
    [Authorize]
    public async Task SendMessage(string streamId, string content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 500)
            return;

        var userId = Context.UserIdentifier ?? string.Empty;
        var msg = await _chat.SaveMessageAsync(streamId, userId, content);

        if (msg is null) return; // потребителят е блокиран или тайм-аут

        // Изпрати до всички в стрийма
        await Clients.Group($"stream:{streamId}")
            .SendAsync("NewMessage", new
            {
                id = msg.Id,
                userId = msg.UserId,
                username = msg.Username,
                avatarUrl = msg.AvatarUrl,
                content = msg.Content,
                color = msg.Color,
                sentAt = msg.SentAt
            });
    }

    /// <summary>Изтриване на съобщение (модератор/admin)</summary>
    [Authorize]
    public async Task DeleteMessage(string streamId, int messageId)
    {
        var userId = Context.UserIdentifier ?? string.Empty;
        var canDelete = await _chat.CanDeleteMessageAsync(userId, messageId);
        if (!canDelete) return;

        await _chat.DeleteMessageAsync(messageId);
        await Clients.Group($"stream:{streamId}")
            .SendAsync("MessageDeleted", messageId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var streamId = await _viewers.OnDisconnectAsync(Context.ConnectionId);

        if (streamId is not null)
        {
            var count = await _viewers.GetCountAsync(streamId);
            await Clients.Group($"stream:{streamId}")
                .SendAsync("ViewerCountUpdated", count);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Хъб за обновяване на информация за стриймовете в реално време
/// </summary>
public class StreamHub : Hub
{
    private readonly IViewerCountService _viewers;

    public StreamHub(IViewerCountService viewers)
    {
        _viewers = viewers;
    }

    /// <summary>Следване на стрийм за нотификации</summary>
    public async Task WatchStream(string streamId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"stream:{streamId}");
        _viewers.TrackConnection(Context.ConnectionId, streamId);

        var count = await _viewers.GetCountAsync(streamId);
        await Clients.Group($"stream:{streamId}")
            .SendAsync("ViewerCountUpdated", count);
    }

    /// <summary>Спиране на следване на стрийм</summary>
    public async Task UnwatchStream(string streamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"stream:{streamId}");
        await _viewers.DecrementAsync(streamId);

        var count = await _viewers.GetCountAsync(streamId);
        await Clients.Group($"stream:{streamId}")
            .SendAsync("ViewerCountUpdated", count);
    }

    [Authorize]
    public async Task StartStream(string streamId)
    {
        await Clients.All.SendAsync("StreamStarted", streamId);
    }

    [Authorize]
    public async Task EndStream(string streamId)
    {
        await Clients.All.SendAsync("StreamEnded", streamId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var streamId = await _viewers.OnDisconnectAsync(Context.ConnectionId);

        if (streamId is not null)
        {
            var count = await _viewers.GetCountAsync(streamId);
            await Clients.Group($"stream:{streamId}")
                .SendAsync("ViewerCountUpdated", count);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Хъб за администраторски панел — реалтайм статистики
/// </summary>
[Authorize(Policy = "AdminOnly")]
public class AdminHub : Hub
{
    private readonly IStreamService _streamService;

    public AdminHub(IStreamService streamService)
    {
        _streamService = streamService;
    }

    public async Task GetLiveStats()
    {
        var stats = await _streamService.GetPlatformStatsAsync();
        await Clients.Caller.SendAsync("StatsUpdated", stats);
    }
}
