using Microsoft.Maui.Controls;

namespace ConcoursCM.ViewModels;

public partial class LoginViewModel(AuthService auth, AlerteService alertes, NavigationService nav) : BaseViewModel
{
    [ObservableProperty] string identifiant = "";
    [ObservableProperty] string motDePasse = "";
    [ObservableProperty] int ongletIndex;     // 0 = Candidat, 1 = École / Inst.

    [RelayCommand]
    async Task ConnexionAsync()
    {
        if (IsBusy) return;
        IsBusy = true; Erreur = null;
        try
        {
            var role = OngletIndex == 1 ? RoleUtilisateur.Institution : RoleUtilisateur.Candidat;
            var r = await auth.ConnexionAsync(Identifiant, MotDePasse, role);
            if (!r.Succes) { Erreur = r.Erreur; return; }

            MotDePasse = "";
            if (auth.Current is { } u) await alertes.SynchroniserAsync(u);
            await nav.GoToAsync("//main/home");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    Task InscriptionAsync() => nav.GoToAsync(OngletIndex == 1 ? "institution-register" : "register");

    [RelayCommand]
    Task GoogleAsync() => nav.AlertAsync("Bientôt disponible",
        "La connexion avec Google nécessite un serveur d'authentification (OAuth). Utilisez votre e-mail ou numéro pour le moment.");

    [RelayCommand]
    Task OublieAsync() => nav.AlertAsync("Mot de passe oublié",
        "La réinitialisation sécurisée nécessite un serveur (envoi de code par e-mail/SMS). " +
        "En attendant, créez un nouveau compte ou contactez le centre d'aide.");
}

public partial class RegisterViewModel(AuthService auth, AlerteService alertes, NavigationService nav) : BaseViewModel
{
    public string[] Regions => Validation.Regions;
    public string SessionTexte => $"Session {DateTime.Today.Year}";

    [ObservableProperty] string nom = "";
    [ObservableProperty] string identifiant = "";
    [ObservableProperty] string? region;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ForceTexte), nameof(ForceColor), nameof(ForceVisible))] string motDePasse = "";
    [ObservableProperty] bool conditions;
    [ObservableProperty] int ongletIndex;

    public bool ForceVisible => MotDePasse.Length > 0;
    public string ForceTexte => $"Sécurité : {Validation.Force(MotDePasse)}";
    public Color ForceColor => Validation.Force(MotDePasse) switch
    {
        ForceMotDePasse.Faible => Palette.Danger,
        ForceMotDePasse.Moyen => Color.FromArgb("#B45309"),
        _ => Color.FromArgb("#16A34A")
    };

    // L'onglet « Institution » redirige vers le formulaire d'accréditation
    partial void OnOngletIndexChanged(int value)
    {
        if (value != 1) return;
        OngletIndex = 0;
        _ = nav.GoToAsync("institution-register");
    }

    [RelayCommand]
    async Task CreerCompteAsync()
    {
        if (IsBusy) return;
        IsBusy = true; Erreur = null;
        try
        {
            var r = await auth.InscrireCandidatAsync(Nom, Identifiant, Region, MotDePasse, Conditions);
            if (!r.Succes) { Erreur = r.Erreur; return; }
            MotDePasse = "";
            if (auth.Current is { } u) await alertes.NotifierServiceSmsAsync(u);
            await nav.ToastAsync("Compte créé. Complétez votre niveau dans Profil pour voir votre éligibilité.");
            await nav.GoToAsync("//main/home");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    Task ConditionsAsync() => nav.AlertAsync("Conditions d'utilisation",
        "• ConcoursCM informe : aucun paiement ni dépôt n'est effectué dans l'application.\n" +
        "• Les candidatures se font uniquement sur les portails officiels.\n" +
        "• Vos données restent sur votre appareil et ne sont jamais revendues.\n" +
        "• Ne communiquez jamais votre mot de passe.");

    [RelayCommand] Task SeConnecterAsync() => nav.GoToAsync("..");
    [RelayCommand] Task ChangerAsync() => nav.GoToAsync("institution-register");
}

public partial class InstitutionRegisterViewModel(AuthService auth, PublicationService publication, NavigationService nav) : BaseViewModel
{
    public string[] Tutelles => Validation.Tutelles;

    [ObservableProperty] string etablissement = "";
    [ObservableProperty] string? tutelle = Validation.Tutelles[0];
    [ObservableProperty] string representant = "";
    [ObservableProperty] string email = "";
    [ObservableProperty] string telephone = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ForceTexte), nameof(ForceColor), nameof(ForceVisible))] string motDePasse = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MandatTexte))] string? mandatPath;
    [ObservableProperty] bool certifie;
    [ObservableProperty] int ongletIndex = 1;

    public string MandatTexte => string.IsNullOrEmpty(MandatPath) ? "Format PDF requis (max. 5 Mo)" : "✓ " + Path.GetFileName(MandatPath);
    public bool ForceVisible => MotDePasse.Length > 0;
    public string ForceTexte => $"Sécurité : {Validation.Force(MotDePasse)}";
    public Color ForceColor => Validation.Force(MotDePasse) switch
    {
        ForceMotDePasse.Faible => Palette.Danger,
        ForceMotDePasse.Moyen => Color.FromArgb("#B45309"),
        _ => Color.FromArgb("#16A34A")
    };

    partial void OnOngletIndexChanged(int value)
    {
        if (value != 0) return;
        OngletIndex = 1;
        _ = nav.GoToAsync("..");
    }

    [RelayCommand]
    async Task ParcourirAsync()
    {
        try
        {
            var f = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Arrêté de nomination ou mandat", FileTypes = FilePickerFileType.Pdf });
            if (f != null) MandatPath = f.FullPath;
        }
        catch (Exception ex) { await nav.AlertAsync("Fichier", ex.Message); }
    }

    [RelayCommand]
    async Task SoumettreAsync()
    {
        if (IsBusy) return;
        IsBusy = true; Erreur = null;
        try
        {
            var r = await auth.InscrireInstitutionAsync(Etablissement, Tutelle, Representant, Email, Telephone, MotDePasse, MandatPath, Certifie);
            if (!r.Succes) { Erreur = r.Erreur; return; }

            await publication.ValiderEnAttenteAsync();   // simulation back-office
            await auth.RafraichirAsync();
            MotDePasse = "";
            await nav.AlertAsync("Demande envoyée",
                "Votre dossier d'accréditation est en cours de vérification (24h ouvrées). " +
                "Vous pourrez publier des annonces dès sa validation.");
            await nav.GoToAsync("//main/home");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] Task SeConnecterAsync() => nav.GoToAsync("..");
}
