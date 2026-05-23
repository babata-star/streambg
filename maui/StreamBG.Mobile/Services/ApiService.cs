using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using StreamBG.Mobile.Models;

namespace StreamBG.Mobile.Services;

public interface IApiService
{
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> RegisterAsync(string username, string email, string password);
    Task<PagedResult<LiveStream>> GetLiveStreamsAsync(string? category = null, int page = 1);
    Task<LiveStream?> GetStreamByUsernameAsync(string username);
    Task<PagedResult<VodVideo>> GetVodsAsync(string? username = null, string? category = null, int page = 1);
    Task<VodVideo?> GetVodByIdAsync(int id);
    Task SaveWatchProgressAsync(int vodId, double positionSeconds, bool completed = false);
    Task<double> GetWatchProgressAsync(int vodId);
    Task<string> GetStreamKeyAsync();
    Task UpdateStreamSettingsAsync(string title, string? description, string? category);
    Task UpdateSocialRelayAsync(SocialRelaySettings settings);
    Task<SearchResults?> SearchAsync(string query, string type = "all", int page = 1);

    // ── Clips ─────────────────────────────────────────────────────────────────
    Task<PagedResult<Clip>> GetClipsAsync(string? streamerId = null, string sort = "newest", int page = 1);
    Task<Clip?> GetClipAsync(int clipId);
    Task<(int ClipId, string? Error)> CreateClipAsync(CreateClipRequest req);
    Task DeleteClipAsync(int clipId);
    Task RecordClipViewAsync(int clipId);
}

public record SocialRelaySettings(
    bool RelayToYouTube, string? YouTubeKey,
    bool RelayToFacebook, string? FacebookKey,
    bool RelayToTikTok, string? TikTokKey);

public class ApiService : IApiService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiService(HttpClient http) => _http = http;

    public void SetAuthToken(string? token)
    {
        _http.DefaultRequestHeaders.Authorization =
            token is not null ? new AuthenticationHeaderValue("Bearer", token) : null;
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var resp = await _http.PostAsJsonAsync("/api/auth/login", new { email, password });
        var content = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            var err = JsonSerializer.Deserialize<ErrorResponse>(content, _json);
            return new AuthResult(false, null, null, null, err?.Error ?? "Грешна парола или имейл");
        }
        var data = JsonSerializer.Deserialize<AuthResponse>(content, _json)!;
        return new AuthResult(true, data.AccessToken, data.RefreshToken, data.User, null);
    }

    public async Task<AuthResult> RegisterAsync(string username, string email, string password)
    {
        var resp = await _http.PostAsJsonAsync("/api/auth/register", new { username, email, password });
        var content = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            var err = JsonSerializer.Deserialize<ErrorResponse>(content, _json);
            return new AuthResult(false, null, null, null, err?.Error ?? "Грешка при регистрация");
        }
        var data = JsonSerializer.Deserialize<AuthResponse>(content, _json)!;
        return new AuthResult(true, data.AccessToken, data.RefreshToken, data.User, null);
    }

    public async Task<PagedResult<LiveStream>> GetLiveStreamsAsync(string? category = null, int page = 1)
    {
        var url = $"/api/streams?page={page}&pageSize=20{(category is not null ? $"&category={Uri.EscapeDataString(category)}" : "")}";
        var resp = await _http.GetFromJsonAsync<PagedResult<LiveStream>>(url, _json);
        return resp ?? new PagedResult<LiveStream>(new(), 0, page, 20);
    }

    public async Task<LiveStream?> GetStreamByUsernameAsync(string username)
    {
        try
        {
            return await _http.GetFromJsonAsync<LiveStream>($"/api/streams/{username}", _json);
        }
        catch { return null; }
    }

    public async Task<PagedResult<VodVideo>> GetVodsAsync(string? username = null, string? category = null, int page = 1)
    {
        var url = $"/api/vod?page={page}&pageSize=20";
        if (username is not null) url += $"&username={Uri.EscapeDataString(username)}";
        if (category is not null) url += $"&category={Uri.EscapeDataString(category)}";
        var resp = await _http.GetFromJsonAsync<PagedResult<VodVideo>>(url, _json);
        return resp ?? new PagedResult<VodVideo>(new(), 0, page, 20);
    }

    public async Task<VodVideo?> GetVodByIdAsync(int id)
    {
        try { return await _http.GetFromJsonAsync<VodVideo>($"/api/vod/{id}", _json); }
        catch { return null; }
    }

    public async Task SaveWatchProgressAsync(int vodId, double positionSeconds, bool completed = false)
    {
        try
        {
            await _http.PostAsJsonAsync($"/api/vod/{vodId}/progress",
                new { positionSeconds, completed });
        }
        catch { /* offline – ignore */ }
    }

    public async Task<double> GetWatchProgressAsync(int vodId)
    {
        try
        {
            var data = await _http.GetFromJsonAsync<ProgressResponse>($"/api/vod/{vodId}/progress", _json);
            return data?.PositionSeconds ?? 0;
        }
        catch { return 0; }
    }

    public async Task<string> GetStreamKeyAsync()
    {
        var data = await _http.GetFromJsonAsync<StreamKeyResponse>("/api/streams/my/key", _json);
        return data?.StreamKey ?? "";
    }

    public async Task UpdateStreamSettingsAsync(string title, string? description, string? category)
    {
        await _http.PutAsJsonAsync("/api/streams/settings", new { title, description, category });
    }

    public async Task UpdateSocialRelayAsync(SocialRelaySettings s)
    {
        await _http.PutAsJsonAsync("/api/streams/my/social", new
        {
            youTubeStreamKey = s.YouTubeKey,
            facebookStreamKey = s.FacebookKey,
            tikTokStreamKey = s.TikTokKey,
            relayToYouTube = s.RelayToYouTube,
            relayToFacebook = s.RelayToFacebook,
            relayToTikTok = s.RelayToTikTok
        });
    }

    public async Task<SearchResults?> SearchAsync(string query, string type = "all", int page = 1)
    {
        try
        {
            var url = $"/api/search?q={Uri.EscapeDataString(query)}&type={type}&page={page}";
            return await _http.GetFromJsonAsync<SearchResults>(url, _json);
        }
        catch { return null; }
    }

    // ── Clips ─────────────────────────────────────────────────────────────────

    public async Task<PagedResult<Clip>> GetClipsAsync(
        string? streamerId = null, string sort = "newest", int page = 1)
    {
        var url = $"/api/clips?sort={sort}&page={page}&pageSize=24";
        if (streamerId is not null) url += $"&streamerId={Uri.EscapeDataString(streamerId)}";
        var resp = await _http.GetFromJsonAsync<PagedResult<Clip>>(url, _json);
        return resp ?? new PagedResult<Clip>(new(), 0, page, 24);
    }

    public async Task<Clip?> GetClipAsync(int clipId)
    {
        try { return await _http.GetFromJsonAsync<Clip>($"/api/clips/{clipId}", _json); }
        catch { return null; }
    }

    public async Task<(int ClipId, string? Error)> CreateClipAsync(CreateClipRequest req)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/clips", req);
            var content = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                var err = JsonSerializer.Deserialize<ErrorResponse>(content, _json);
                return (0, err?.Error ?? "Грешка при създаване на клип");
            }
            var data = JsonSerializer.Deserialize<ClipCreatedResponse>(content, _json);
            return (data?.ClipId ?? 0, null);
        }
        catch (Exception ex) { return (0, ex.Message); }
    }

    public async Task DeleteClipAsync(int clipId)
    {
        try { await _http.DeleteAsync($"/api/clips/{clipId}"); }
        catch { /* offline */ }
    }

    public async Task RecordClipViewAsync(int clipId)
    {
        try { await _http.PostAsync($"/api/clips/{clipId}/view", null); }
        catch { /* fire-and-forget */ }
    }

    private record AuthResponse(string AccessToken, string RefreshToken, User User);
    private record ErrorResponse(string Error);
    private record StreamKeyResponse(string StreamKey);
    private record ProgressResponse(double PositionSeconds, bool Completed);
    private record ClipCreatedResponse(int ClipId, string Message);
}
