using ConcoursCM.ViewModels;

namespace ConcoursCM.Views;

public partial class CandidaturesPage : ContentPage
{
    readonly CandidaturesViewModel _vm;

    public CandidaturesPage(CandidaturesViewModel vm)
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
