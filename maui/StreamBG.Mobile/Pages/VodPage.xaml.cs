using StreamBG.Mobile.ViewModels;

namespace StreamBG.Mobile.Pages;

public partial class VodPage : ContentPage
{
    public VodPage(VodViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is VodViewModel vm)
            vm.Dispose();
    }
}
