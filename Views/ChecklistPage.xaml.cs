using ConcoursCM.ViewModels;

namespace ConcoursCM.Views;

public partial class ChecklistPage : ContentPage
{
    readonly ChecklistViewModel _vm;

    public ChecklistPage(ChecklistViewModel vm)
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
