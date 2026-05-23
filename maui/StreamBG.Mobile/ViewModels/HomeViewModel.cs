using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using StreamBG.Mobile.Models;
using StreamBG.Mobile.Services;

namespace StreamBG.Mobile.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly IApiService _api;

    [ObservableProperty] bool isLoading;
    [ObservableProperty] bool isRefreshing;
    [ObservableProperty] string selectedCategory = "Всички";
    [ObservableProperty] int selectedTab; // 0 = Live, 1 = VOD

    public ObservableCollection<LiveStream> LiveStreams { get; } = new();
    public ObservableCollection<VodVideo> VodVideos { get; } = new();

    public string[] Categories { get; } = new[]
    {
        "Всички", "Игри", "Музика", "Изкуство", "Спорт",
        "Образование", "Технологии", "Готвене"
    };

    private int _livePage = 1, _vodPage = 1;
    private bool _liveHasMore = true, _vodHasMore = true;

    public HomeViewModel(IApiService api)
    {
        _api = api;
    }

    [RelayCommand]
    async Task LoadAsync()
    {
        IsLoading = true;
        _livePage = 1; _vodPage = 1;
        _liveHasMore = true; _vodHasMore = true;
        LiveStreams.Clear();
        VodVideos.Clear();

        await Task.WhenAll(LoadLiveAsync(), LoadVodAsync());
        IsLoading = false;
    }

    [RelayCommand]
    async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    async Task SelectCategoryAsync(string cat)
    {
        SelectedCategory = cat;
        await LoadAsync();
    }

    [RelayCommand]
    async Task LoadMoreLiveAsync()
    {
        if (!_liveHasMore || IsLoading) return;
        _livePage++;
        await LoadLiveAsync();
    }

    [RelayCommand]
    async Task LoadMoreVodAsync()
    {
        if (!_vodHasMore || IsLoading) return;
        _vodPage++;
        await LoadVodAsync();
    }

    [RelayCommand]
    void SelectTab(string tab)
    {
        if (int.TryParse(tab, out var i))
            SelectedTab = i;
    }

    [RelayCommand]
    async Task OpenStreamAsync(LiveStream stream)
    {
        await Shell.Current.GoToAsync("stream",
            new Dictionary<string, object> { ["stream"] = stream });
    }

    [RelayCommand]
    async Task OpenVodAsync(VodVideo vod)
    {
        await Shell.Current.GoToAsync("vod",
            new Dictionary<string, object> { ["vod"] = vod });
    }

    private async Task LoadLiveAsync()
    {
        var cat = SelectedCategory == "Всички" ? null : SelectedCategory;
        var result = await _api.GetLiveStreamsAsync(cat, _livePage);
        foreach (var s in result.Items) LiveStreams.Add(s);
        _liveHasMore = LiveStreams.Count < result.Total;
    }

    private async Task LoadVodAsync()
    {
        var cat = SelectedCategory == "Всички" ? null : SelectedCategory;
        var result = await _api.GetVodsAsync(category: cat, page: _vodPage);
        foreach (var v in result.Items) VodVideos.Add(v);
        _vodHasMore = VodVideos.Count < result.Total;
    }
}
