using ConcoursCM.ViewModels;

namespace ConcoursCM.Views;

public partial class ProfilPage : ContentPage
{
    readonly ProfilViewModel _vm;

    public ProfilPage(ProfilViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.ChargerCommand.ExecuteAsync(null); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }
}
