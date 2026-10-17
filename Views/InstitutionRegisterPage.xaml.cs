using ConcoursCM.ViewModels;

namespace ConcoursCM.Views;

public partial class InstitutionRegisterPage : ContentPage
{
    readonly InstitutionRegisterViewModel _vm;

    public InstitutionRegisterPage(InstitutionRegisterViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }
}
