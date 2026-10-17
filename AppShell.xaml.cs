using ConcoursCM.Views;

namespace ConcoursCM;

public partial class AppShell : Shell
{
    readonly AuthService _auth;
    readonly PublicationService _publication;
    readonly AlerteService _alertes;
    bool _demarre;

    public AppShell(AuthService auth, PublicationService publication, AlerteService alertes)
    {
        InitializeComponent();
        _auth = auth;
        _publication = publication;
        _alertes = alertes;

        // Pages "poussées" (hors barre du bas)
        Routing.RegisterRoute("register", typeof(RegisterPage));
        Routing.RegisterRoute("institution-register", typeof(InstitutionRegisterPage));
        Routing.RegisterRoute("concours-detail", typeof(ConcoursDetailPage));
        Routing.RegisterRoute("checklist", typeof(ChecklistPage));
        Routing.RegisterRoute("candidatures", typeof(CandidaturesPage));
        Routing.RegisterRoute("alertes", typeof(AlertesPage));
        Routing.RegisterRoute("publish", typeof(PublishPage));

        Loaded += OnLoaded;
    }

    async void OnLoaded(object? sender, EventArgs e)
    {
        if (_demarre) return;
        _demarre = true;
        try
        {
            // Simule la modération back-office (validation des annonces / accréditations)
            await _publication.ValiderEnAttenteAsync();

            // Session persistante : on évite de redemander le mot de passe à chaque ouverture
            if (await _auth.RestaurerSessionAsync() && _auth.Current is { } u)
            {
                await _alertes.SynchroniserAsync(u);
                await GoToAsync("//main/home");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
