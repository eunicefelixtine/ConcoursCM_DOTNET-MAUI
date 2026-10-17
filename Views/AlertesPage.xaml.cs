using ConcoursCM.ViewModels;

namespace ConcoursCM.Views;

public partial class AlertesPage : ContentPage
{
    readonly AlertesViewModel _vm;

    public AlertesPage(AlertesViewModel vm)
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
