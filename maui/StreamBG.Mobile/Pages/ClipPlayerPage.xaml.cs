using StreamBG.Mobile.ViewModels;

namespace StreamBG.Mobile.Pages;

public partial class ClipPlayerPage : ContentPage
{
    private readonly ClipPlayerViewModel _vm;

    public ClipPlayerPage(ClipPlayerViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        VideoView.MediaPlayer = vm.MediaPlayer;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Dispose();
    }
}
