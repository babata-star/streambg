using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamBG.Mobile.Services;

namespace StreamBG.Mobile.ViewModels;

public partial class AuthViewModel : ObservableObject
{
    private readonly IApiService _api;

    [ObservableProperty] string email = string.Empty;
    [ObservableProperty] string password = string.Empty;
    [ObservableProperty] string username = string.Empty;
    [ObservableProperty] string? errorMessage;
    [ObservableProperty] bool isLoading;
    [ObservableProperty] bool isLoginMode = true;

    public AuthViewModel(IApiService api) => _api = api;

    [RelayCommand]
    async Task SubmitAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Въведи имейл и парола";
            return;
        }

        IsLoading = true;
        try
        {
            var result = IsLoginMode
                ? await _api.LoginAsync(Email, Password)
                : await _api.RegisterAsync(Username, Email, Password);

            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            // Запази токена и потребителя
            Preferences.Set("access_token", result.AccessToken);
            Preferences.Set("username", result.User!.Username);
            Preferences.Set("user_id", result.User!.Id);
            Preferences.Set("is_streamer", result.User!.IsStreamer);
            Preferences.Set("is_admin", result.User!.IsAdmin);

            ((ApiService)_api).SetAuthToken(result.AccessToken);

            // Навигирай към главната страница
            await Shell.Current.GoToAsync("//main");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Мрежова грешка — провери интернет връзката";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    void ToggleMode()
    {
        IsLoginMode = !IsLoginMode;
        ErrorMessage = null;
    }
}
