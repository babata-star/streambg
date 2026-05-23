using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamBG.Mobile.Services;

namespace StreamBG.Mobile.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly IApiService _api;

    [ObservableProperty] string username = string.Empty;
    [ObservableProperty] string? streamKey;
    [ObservableProperty] string streamTitle = string.Empty;
    [ObservableProperty] string? streamDescription;
    [ObservableProperty] string? streamCategory;
    [ObservableProperty] bool isStreamer;
    [ObservableProperty] bool isLoading;
    [ObservableProperty] bool isSaving;
    [ObservableProperty] string? statusMessage;
    [ObservableProperty] bool isLoggedIn;

    public ProfileViewModel(IApiService api) => _api = api;

    [RelayCommand]
    async Task LoadAsync()
    {
        var token = Preferences.Get("access_token", (string?)null);
        IsLoggedIn = token is not null;
        if (!IsLoggedIn) return;

        Username   = Preferences.Get("username", string.Empty)!;
        IsStreamer = Preferences.Get("is_streamer", false);

        if (!IsStreamer) return;

        IsLoading = true;
        try { StreamKey = await _api.GetStreamKeyAsync(); }
        catch { StreamKey = "—"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    async Task SaveSettingsAsync()
    {
        IsSaving = true;
        StatusMessage = null;
        try
        {
            await _api.UpdateStreamSettingsAsync(StreamTitle, StreamDescription, StreamCategory);
            StatusMessage = "Настройките са запазени ✓";
        }
        catch
        {
            StatusMessage = "Грешка при запазване";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    async Task LogoutAsync()
    {
        Preferences.Remove("access_token");
        Preferences.Remove("username");
        Preferences.Remove("user_id");
        Preferences.Remove("is_streamer");
        Preferences.Remove("is_admin");

        await Shell.Current.GoToAsync("//auth");
    }
}
