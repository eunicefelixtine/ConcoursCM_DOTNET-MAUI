namespace ConcoursCM;

public partial class App : Application
{
    readonly AppShell _shell;

    public App(AppShell shell, SettingsService settings)
    {
        InitializeComponent();
        _shell = shell;
        settings.AppliquerTheme();
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(_shell);
}
