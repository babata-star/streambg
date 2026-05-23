using StreamBG.Mobile.ViewModels;

namespace StreamBG.Mobile.Pages;

public partial class SearchPage : ContentPage
{
    public SearchPage(SearchViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
