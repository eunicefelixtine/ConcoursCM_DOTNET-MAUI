using ConcoursCM.ViewModels;

namespace ConcoursCM.Views;

public partial class RegisterPage : ContentPage
{
    readonly RegisterViewModel _vm;

    public RegisterPage(RegisterViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }
}
