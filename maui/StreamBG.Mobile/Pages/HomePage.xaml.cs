using StreamBG.Mobile.ViewModels;

namespace StreamBG.Mobile.Pages;

public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is HomeViewModel vm && !vm.IsLoading)
            vm.LoadCommand.Execute(null);
    }
}
