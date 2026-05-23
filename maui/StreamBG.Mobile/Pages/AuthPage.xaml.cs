using StreamBG.Mobile.ViewModels;

namespace StreamBG.Mobile.Pages;

public partial class AuthPage : ContentPage
{
    public AuthPage(AuthViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
