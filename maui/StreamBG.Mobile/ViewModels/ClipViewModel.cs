using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamBG.Mobile.Models;
using StreamBG.Mobile.Services;

namespace StreamBG.Mobile.ViewModels;

public partial class ClipViewModel : ObservableObject
{
    private readonly IApiService _api;
    private int _page = 1;
    private bool _hasMore = true;

    [ObservableProperty] ObservableCollection<Clip> clips = new();
    [ObservableProperty] bool isLoading;
    [ObservableProperty] bool isRefreshing;
    [ObservableProperty] string selectedSort = "newest";

    public ClipViewModel(IApiService api) => _api = api;

    // ── Load (first page) ─────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        _page = 1;
        _hasMore = true;
        try
        {
            var result = await _api.GetClipsAsync(sort: SelectedSort, page: 1);
            Clips.Clear();
            foreach (var c in result.Items) Clips.Add(c);
            _hasMore = result.Items.Count >= result.PageSize;
        }
        catch { /* offline */ }
        finally { IsLoading = false; }
    }

    // ── Pull-to-refresh ───────────────────────────────────────────────────────

    [RelayCommand]
    async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadAsync();
        IsRefreshing = false;
    }

    // ── Infinite scroll ───────────────────────────────────────────────────────

    [RelayCommand]
    async Task LoadMoreAsync()
    {
        if (!_hasMore || IsLoading) return;
        IsLoading = true;
        _page++;
        try
        {
            var result = await _api.GetClipsAsync(sort: SelectedSort, page: _page);
            foreach (var c in result.Items) Clips.Add(c);
            _hasMore = result.Items.Count >= result.PageSize;
        }
        catch { /* offline */ }
        finally { IsLoading = false; }
    }

    // ── Open clip player ──────────────────────────────────────────────────────

    [RelayCommand]
    async Task OpenClipAsync(Clip clip)
    {
        await Shell.Current.GoToAsync("clip_player",
            new Dictionary<string, object> { ["clip"] = clip });
    }

    // ── Sort chips ────────────────────────────────────────────────────────────

    [RelayCommand]
    async Task ChangeSortAsync(string sort)
    {
        if (SelectedSort == sort) return;
        SelectedSort = sort;
        await LoadAsync();
    }

    // Computed helpers for sort chip highlight
    public bool SortIsNewest  => SelectedSort == "newest";
    public bool SortIsPopular => SelectedSort == "popular";
}
