using StreamBG.Mobile.ViewModels;

namespace StreamBG.Mobile.Pages;

public partial class ClipPage : ContentPage
{
    private readonly ClipViewModel _vm;

    public ClipPage(ClipViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_vm.Clips.Any())
            _vm.LoadCommand.Execute(null);
    }
}
