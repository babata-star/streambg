using StreamBG.Mobile.ViewModels;

namespace StreamBG.Mobile.Pages;

public partial class StreamPage : ContentPage
{
    public StreamPage(StreamViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is StreamViewModel vm)
            vm.Dispose();
    }
}
