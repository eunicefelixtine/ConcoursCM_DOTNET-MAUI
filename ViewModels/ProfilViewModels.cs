
namespace ConcoursCM.ViewModels;

public partial class ProfilViewModel(AuthService auth, SettingsService settings, ChecklistService checklist, AlerteService alertes,
    PublicationService publication, NavigationService nav) : BaseViewModel
{
    const string EmailSupport = "support@concourscm.cm"; // À remplacer par l'adresse réelle du support

    bool _chargement;
    public ObservableCollection<AnnonceVm> Annonces { get; } = new();

    [ObservableProperty] string nom = "";
    [ObservableProperty] string avatar = "";
    [ObservableProperty] bool verifie;
    [ObservableProperty] string sousTitre = "";
    [ObservableProperty] string regionTexte = "";
    [ObservableProperty] int nbSuivis;
    [ObservableProperty] int nbNonLus;
    [ObservableProperty] bool estInstitution;
    [ObservableProperty] bool estCandidat;
    [ObservableProperty] string? accreditationTexte;
    [ObservableProperty] bool peutPublier;
    [ObservableProperty] bool smsActif;
    [ObservableProperty] bool lowData;
    [ObservableProperty] int themeIndex;

    public string SuivisTexte => NbSuivis == 0 ? "Aucun dossier suivi pour le moment" : $"{NbSuivis} dossier{(NbSuivis > 1 ? "s" : "")} en cours de préparation";
    public bool AAnnonces => Annonces.Count > 0;

    [RelayCommand]
    async Task ChargerAsync()
    {
        if (auth.Current is not { } u) return;
        await auth.RafraichirAsync();
        u = auth.Current!;
        _chargement = true;
        try
        {
            Nom = u.EstInstitution ? u.NomEtablissement : u.NomComplet;
            Avatar = u.Initiales;
            Verifie = u.Verifie;
            EstInstitution = u.EstInstitution;
            EstCandidat = !u.EstInstitution;
            SousTitre = u.EstInstitution ? $"Institution • {u.Tutelle}"
                : $"Candidat{(string.IsNullOrEmpty(u.Niveau) ? " • Niveau à renseigner" : $" • {u.Niveau}{(u.AnneeObtention > 0 ? " " + u.AnneeObtention : "")}")}";
            RegionTexte = string.IsNullOrEmpty(u.Region) ? "" : $"Région {(u.Region == "Centre" ? "du Centre" : u.Region)}";
            SmsActif = u.SmsActif;
            LowData = settings.LowData;
            ThemeIndex = settings.ThemeIndex;
            PeutPublier = u.PeutPublier;
            AccreditationTexte = u.EstInstitution && !u.PeutPublier ? "Accréditation en cours de vérification (24h ouvrées)" : null;

            NbSuivis = u.EstInstitution ? 0 : await checklist.NbSuivisAsync(u.Id);
            NbNonLus = await alertes.NbNonLusAsync(u.Id);
            OnPropertyChanged(nameof(SuivisTexte));

            Annonces.Clear();
            if (u.EstInstitution) foreach (var a in await publication.MesAnnoncesAsync(u.Id)) Annonces.Add(new AnnonceVm(a));
            OnPropertyChanged(nameof(AAnnonces));
        }
        finally { _chargement = false; }
    }

    partial void OnLowDataChanged(bool value) { if (!_chargement) settings.LowData = value; }
    partial void OnThemeIndexChanged(int value) { if (!_chargement) settings.ThemeIndex = value; }

    partial void OnSmsActifChanged(bool value) => _ = AppliquerSmsAsync(value);

    async Task AppliquerSmsAsync(bool actif)
    {
        if (_chargement || auth.Current is not { } u) return;
        u.SmsActif = actif;
        if (actif && string.IsNullOrEmpty(u.NumeroSms))
        {
            await ModifierNumeroAsync();
            if (string.IsNullOrEmpty(auth.Current?.NumeroSms)) { _chargement = true; SmsActif = false; _chargement = false; u.SmsActif = false; }
        }
        await auth.MettreAJourAsync(u);
        if (actif)
        {
            await alertes.ActiverNotificationsAsync();
            await alertes.NotifierServiceSmsAsync(u);
            await alertes.SynchroniserAsync(u);
        }
    }

    [RelayCommand]
    async Task ModifierNumeroAsync()
    {
        if (auth.Current is not { } u) return;
        var saisie = await nav.PromptAsync("Numéro de rappel", "Numéro camerounais (MTN / Orange / Nexttel)", u.NumeroSms, "6XX XX XX XX", Keyboard.Telephone);
        if (saisie == null) return;
        if (!Validation.TryNormaliserIdentifiant(saisie, out var n, out var tel) || !tel)
        { await nav.AlertAsync("Numéro invalide", "Saisissez un numéro camerounais valide (ex. 6 94 12 34 56)."); return; }
        u.NumeroSms = n;
        await auth.MettreAJourAsync(u);
        await alertes.NotifierServiceSmsAsync(u);
        await nav.ToastAsync("Numéro enregistré");
    }

    [RelayCommand]
    async Task ModifierNiveauAsync()
    {
        if (auth.Current is not { } u || u.EstInstitution) return;
        var niveau = await nav.ActionSheetAsync("Votre niveau d'études", "Annuler", Validation.Niveaux);
        if (niveau == null || niveau == "Annuler") return;
        var annee = await nav.PromptAsync("Année d'obtention", "Ex. 2024", u.AnneeObtention > 0 ? u.AnneeObtention.ToString() : "", "AAAA", Keyboard.Numeric);
        u.Niveau = niveau;
        if (int.TryParse(annee, out var a) && a is > 1990 and <= 2100) u.AnneeObtention = a;
        await auth.MettreAJourAsync(u);
        await ChargerAsync();
        await nav.ToastAsync("Profil mis à jour : l'éligibilité est recalculée");
    }

    [RelayCommand] Task CandidaturesAsync() => nav.GoToAsync("candidatures");
    [RelayCommand] Task PublierAsync() => nav.GoToAsync("publish");

    [RelayCommand]
    async Task AideAsync()
    {
        try { await Launcher.Default.OpenAsync($"mailto:{EmailSupport}?subject=Aide%20ConcoursCM"); }
        catch { await nav.AlertAsync("Centre d'aide", $"Écrivez-nous à {EmailSupport}"); }
    }

    [RelayCommand]
    async Task DeconnexionAsync()
    {
        if (!await nav.ConfirmAsync("Se déconnecter", "Voulez-vous vraiment vous déconnecter ?", "Se déconnecter")) return;
        auth.Deconnexion();
        await nav.GoToAsync("//login");
    }
}

public partial class AlertesViewModel(AuthService auth, SettingsService settings, AlerteService alertes, ConcoursService svc, NavigationService nav) : BaseViewModel
{
    bool _chargement;
    public ObservableCollection<AlerteVm> Items { get; } = new();

    [ObservableProperty] int nbNonLus;
    [ObservableProperty] bool vide;
    [ObservableProperty] bool prefsVisibles;
    [ObservableProperty] bool rappel7;
    [ObservableProperty] bool rappel3;
    [ObservableProperty] bool rappel1;
    [ObservableProperty] string avatar = "";

    [RelayCommand]
    async Task ChargerAsync()
    {
        if (auth.Current is not { } u) return;
        _chargement = true;
        Avatar = u.Initiales;
        Rappel7 = settings.Rappel7; Rappel3 = settings.Rappel3; Rappel1 = settings.Rappel1;
        _chargement = false;

        await alertes.SynchroniserAsync(u);
        Items.Clear();
        foreach (var a in await alertes.ListerAsync(u.Id)) Items.Add(new AlerteVm(a));
        NbNonLus = Items.Count(i => i.NonLu);
        Vide = Items.Count == 0;
    }

    partial void OnRappel7Changed(bool value) { if (!_chargement) { settings.Rappel7 = value; _ = ResynchroniserAsync(); } }
    partial void OnRappel3Changed(bool value) { if (!_chargement) { settings.Rappel3 = value; _ = ResynchroniserAsync(); } }
    partial void OnRappel1Changed(bool value) { if (!_chargement) { settings.Rappel1 = value; _ = ResynchroniserAsync(); } }

    async Task ResynchroniserAsync()
    {
        if (auth.Current is { } u) await alertes.SynchroniserAsync(u);
    }

    [RelayCommand] void BasculerPrefs() => PrefsVisibles = !PrefsVisibles;

    [RelayCommand]
    async Task ToutMarquerLuAsync()
    {
        if (auth.Current is not { } u) return;
        await alertes.MarquerToutLuAsync(u.Id);
        await ChargerAsync();
    }

    [RelayCommand]
    async Task OuvrirAsync(AlerteVm vm)
    {
        await alertes.MarquerLuAsync(vm.A);
        switch (vm.A.Type)
        {
            case TypeAlerte.Rappel:
                await nav.GoToAsync($"checklist?id={vm.A.CibleId}"); break;
            case TypeAlerte.ArreteOfficiel:
                await nav.GoToAsync($"concours-detail?id={vm.A.CibleId}"); break;
            case TypeAlerte.Bourse:
                var b = await svc.ObtenirBourseAsync(vm.A.CibleId);
                if (b != null) await nav.OuvrirLienOfficielAsync(b.LienOfficiel);
                break;
            case TypeAlerte.Service:
                await nav.GoToAsync("//main/profil"); break;
        }
    }
}

public partial class PublishViewModel(AuthService auth, PublicationService publication, NavigationService nav) : BaseViewModel
{
    public string[] Portees => ["National", "International", "Partenariat"];
    public DateTime DateMin => DateTime.Today.AddDays(3);

    [ObservableProperty] int ongletIndex;          // 0 = Concours, 1 = Bourse
    [ObservableProperty, NotifyPropertyChangedFor(nameof(EstConcours))] bool estBourse;
    [ObservableProperty] string etablissement = "";
    [ObservableProperty] string tutelle = "";
    [ObservableProperty] string intitule = "";
    [ObservableProperty] DateTime dateCloture = DateTime.Today.AddDays(30);
    [ObservableProperty] DateTime dateEpreuves = DateTime.Today.AddDays(60);
    [ObservableProperty] string places = "";
    [ObservableProperty] string frais = "";
    [ObservableProperty] string lien = "";
    [ObservableProperty] string reference = "";
    [ObservableProperty] string? portee = "National";
    [ObservableProperty] string avantages = "";
    [ObservableProperty] string niveauCible = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ArreteTexte))] string? arretePath;
    [ObservableProperty] bool peutPublier;
    [ObservableProperty] string? bandeau;
    [ObservableProperty] string avatar = "";

    public bool EstConcours => !EstBourse;
    public string ArreteTexte => string.IsNullOrEmpty(ArretePath) ? "Sélectionner le document officiel (.pdf)" : "✓ " + Path.GetFileName(ArretePath);

    partial void OnOngletIndexChanged(int value) => EstBourse = value == 1;

    [RelayCommand]
    async Task ChargerAsync()
    {
        if (auth.Current is not { } u) return;
        await auth.RafraichirAsync();
        u = auth.Current!;
        Avatar = u.Initiales;
        Etablissement = u.NomEtablissement;
        Tutelle = u.Tutelle.Split('—')[0].Trim();
        PeutPublier = u.PeutPublier;
        Bandeau = !u.EstInstitution ? "Seules les institutions accréditées peuvent publier."
            : u.PeutPublier ? null : "Accréditation en cours de vérification : la publication sera disponible dès sa validation.";
    }

    [RelayCommand]
    async Task ParcourirAsync()
    {
        try
        {
            var f = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Arrêté officiel signé", FileTypes = FilePickerFileType.Pdf });
            if (f != null) ArretePath = f.FullPath;
        }
        catch (Exception ex) { await nav.AlertAsync("Fichier", ex.Message); }
    }

    static int Nombre(string? s) => int.TryParse(new string((s ?? "").Where(char.IsDigit).ToArray()), out var n) ? n : 0;

    [RelayCommand]
    async Task PublierAsync()
    {
        if (auth.Current is not { } u || IsBusy) return;
        IsBusy = true; Erreur = null;
        try
        {
            var r = await publication.SoumettreAsync(u, new PublicationForm
            {
                EstBourse = EstBourse, Intitule = Intitule, DateCloture = DateCloture, DateEpreuves = DateEpreuves,
                Places = Nombre(Places), FraisFcfa = Nombre(Frais), Lien = Lien, ArreteSource = ArretePath ?? "",
                Reference = Reference, Portee = Portee ?? "National", Avantages = Avantages, NiveauCible = NiveauCible
            });
            if (!r.Succes) { Erreur = r.Erreur; return; }

            await nav.ToastAsync("Dossier soumis pour validation ! Votre avis sera visible sous 24h ouvrées.");
            Intitule = ""; Places = ""; Frais = ""; Lien = ""; Reference = ""; Avantages = ""; ArretePath = null;
            await nav.GoToAsync("//main/profil");
        }
        finally { IsBusy = false; }
    }
}
