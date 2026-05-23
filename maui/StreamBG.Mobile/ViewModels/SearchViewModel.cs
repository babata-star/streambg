using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamBG.Mobile.Models;
using StreamBG.Mobile.Services;

namespace StreamBG.Mobile.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly IApiService _api;

    [ObservableProperty] string query = string.Empty;
    [ObservableProperty] bool isLoading;
    [ObservableProperty] bool hasResults;
    [ObservableProperty] SearchResults? results;

    public SearchViewModel(IApiService api) => _api = api;

    [RelayCommand]
    async Task SearchAsync()
    {
        var q = Query.Trim();
        if (q.Length < 2) return;

        IsLoading = true;
        HasResults = false;
        Results = null;

        try
        {
            Results = await _api.SearchAsync(q);
            HasResults = Results is not null;
        }
        catch { /* ignore network errors */ }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    async Task OpenStreamAsync(LiveStream stream)
        => await Shell.Current.GoToAsync("stream",
            new Dictionary<string, object> { ["stream"] = stream });

    [RelayCommand]
    async Task OpenVodAsync(VodVideo vod)
        => await Shell.Current.GoToAsync("vod",
            new Dictionary<string, object> { ["vod"] = vod });
}
