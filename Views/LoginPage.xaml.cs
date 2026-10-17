using ConcoursCM.ViewModels;

namespace ConcoursCM.Views;

public partial class LoginPage : ContentPage
{
    readonly LoginViewModel _vm;

    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }
}
