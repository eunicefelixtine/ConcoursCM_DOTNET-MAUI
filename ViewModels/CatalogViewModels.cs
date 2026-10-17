namespace ConcoursCM.ViewModels;

public partial class HomeViewModel(AuthService auth, ConcoursService svc, AlerteService alertes, NavigationService nav, SearchContext search) : BaseViewModel
{
    public ObservableCollection<UneItemVm> Unes { get; } = new();

    [ObservableProperty] string salutation = "";
    [ObservableProperty] string sessionTexte = "";
    [ObservableProperty] string avatar = "";
    [ObservableProperty] int nbNonLus;
    [ObservableProperty] int nbOuverts;
    [ObservableProperty] int nbNouveaux;
    [ObservableProperty] int nbBourses;
    [ObservableProperty] string recherche = "";
    [ObservableProperty] bool estInstitution;
    [ObservableProperty] bool aucuneUne;
    [ObservableProperty] string? statutInstitution;

    public string NouveauxTexte => NbNouveaux > 0 ? $"+{NbNouveaux} new" : "À jour";

    [RelayCommand]
    async Task ChargerAsync()
    {
        if (auth.Current is not { } u) return;
        await auth.RafraichirAsync();
        u = auth.Current!;
        EstInstitution = u.EstInstitution;
        Avatar = u.Initiales;
        Salutation = u.EstInstitution ? $"Bonjour, {u.NomEtablissement} 👋" : $"Bonjour, {u.Prenom} 👋";
        SessionTexte = Fmt.AnneeAcademique(DateTime.Today);
        StatutInstitution = !u.EstInstitution ? null
            : u.PeutPublier ? null : "Accréditation en cours de vérification (24h ouvrées). La publication sera disponible dès validation.";

        var stats = await svc.StatsAsync();
        NbOuverts = stats.Ouverts; NbNouveaux = stats.Nouveaux; NbBourses = stats.Bourses;
        OnPropertyChanged(nameof(NouveauxTexte));
        NbNonLus = await alertes.NbNonLusAsync(u.Id);

        Unes.Clear();
        foreach (var i in await svc.UnesAsync()) Unes.Add(new UneItemVm(i));
        AucuneUne = Unes.Count == 0;
    }

    [RelayCommand]
    async Task RechercherAsync()
    {
        search.Pending = Recherche;
        Recherche = "";
        await nav.GoToAsync("//main/concours");
    }

    [RelayCommand] Task AllerConcoursAsync() => nav.GoToAsync("//main/concours");
    [RelayCommand] Task AllerBoursesAsync() => nav.GoToAsync("//main/bourses");
    [RelayCommand] Task PublierAsync() => nav.GoToAsync("publish");

    [RelayCommand]
    async Task OuvrirUneAsync(UneItemVm i)
    {
        if (i.Item.Cible == TypeCible.Concours) { await nav.GoToAsync($"concours-detail?id={i.Id}"); return; }
        var b = await svc.ObtenirBourseAsync(i.Id);
        if (b != null) await nav.OuvrirLienOfficielAsync(b.LienOfficiel);
    }
}

public partial class ConcoursListViewModel(AuthService auth, ConcoursService svc, AlerteService alertes, NavigationService nav, SearchContext search) : BaseViewModel
{
    public ObservableCollection<ConcoursCardVm> Concours { get; } = new();
    public ObservableCollection<ChipVm> Chips { get; } = new();

    [ObservableProperty] string recherche = "";
    [ObservableProperty] string sessionTexte = "";
    [ObservableProperty] string avatar = "";
    [ObservableProperty] int nbNonLus;
    [ObservableProperty] int nbOuverts;
    [ObservableProperty] bool vide;
    [ObservableProperty] bool aFiltres;

    string _categorie = "Tous";
    bool _seulementOuverts = true, _seulementEligibles;
    TriConcours _tri = TriConcours.Cloture;
    CancellationTokenSource? _cts;

    partial void OnRechercheChanged(string value) => _ = RechercheDiffereeAsync();

    async Task RechercheDiffereeAsync()
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        try { await Task.Delay(300, cts.Token); await ChargerListeAsync(); }
        catch (TaskCanceledException) { }
    }

    [RelayCommand]
    async Task ChargerAsync()
    {
        if (auth.Current is not { } u) return;
        Avatar = u.Initiales;
        SessionTexte = $"SESSION {Fmt.AnneeAcademique(DateTime.Today)}";
        NbNonLus = await alertes.NbNonLusAsync(u.Id);

        if (Chips.Count == 0)
        {
            Chips.Add(new ChipVm("Tous", true));
            foreach (var c in await svc.CategoriesConcoursAsync()) Chips.Add(new ChipVm(c, false));
        }
        if (search.Pending is { } p)
        {
            search.Pending = null;
            if (Recherche != p) { Recherche = p; return; } // déclenche le chargement différé
        }
        await ChargerListeAsync();
    }

    async Task ChargerListeAsync()
    {
        if (auth.Current is not { } u) return;
        var liste = await svc.ListerConcoursAsync(Recherche, _categorie, _seulementOuverts, _seulementEligibles, u, _tri);
        var favoris = await svc.FavorisAsync(u.Id, TypeCible.Concours);
        Concours.Clear();
        foreach (var c in liste) Concours.Add(new ConcoursCardVm(c, favoris.Contains(c.Id), ConcoursService.Evaluer(u, c)));
        NbOuverts = liste.Count(c => !c.EstClos);
        Vide = Concours.Count == 0;
        AFiltres = !_seulementOuverts || _seulementEligibles || _tri != TriConcours.Cloture;
    }

    [RelayCommand]
    async Task ChoisirCategorieAsync(ChipVm chip)
    {
        foreach (var c in Chips) c.EstSelectionne = c == chip;
        _categorie = chip.Label;
        await ChargerListeAsync();
    }

    [RelayCommand]
    async Task FiltresAsync()
    {
        var o1 = _seulementOuverts ? "✓ Concours ouverts uniquement" : "Concours ouverts uniquement";
        var o2 = _seulementEligibles ? "✓ Compatibles avec mon niveau" : "Compatibles avec mon niveau";
        var choix = await nav.ActionSheetAsync("Filtrer & trier", "Fermer", "Trier par date de clôture", "Trier par plus récents", "Trier de A à Z", o1, o2);
        if (choix == null || choix == "Fermer") return;
        if (choix.StartsWith("Trier par date")) _tri = TriConcours.Cloture;
        else if (choix.StartsWith("Trier par plus")) _tri = TriConcours.Recents;
        else if (choix.StartsWith("Trier de A")) _tri = TriConcours.Alphabetique;
        else if (choix.Contains("ouverts")) _seulementOuverts = !_seulementOuverts;
        else if (choix.Contains("niveau"))
        {
            if (string.IsNullOrEmpty(auth.Current?.Niveau))
                await nav.AlertAsync("Profil incomplet", "Renseignez votre niveau (ex. Bac C) dans l'onglet Profil pour filtrer par éligibilité.");
            else _seulementEligibles = !_seulementEligibles;
        }
        await ChargerListeAsync();
    }

    [RelayCommand]
    async Task BasculerFavoriAsync(ConcoursCardVm card)
    {
        if (auth.Current is not { } u) return;
        card.EstFavori = await svc.ToggleFavoriAsync(u.Id, TypeCible.Concours, card.Id);
        if (card.EstFavori) { await alertes.SynchroniserAsync(u); await nav.ToastAsync("Ajouté à vos favoris : rappels activés"); }
    }

    [RelayCommand] Task OuvrirAsync(ConcoursCardVm card) => nav.GoToAsync($"concours-detail?id={card.Id}");

    [RelayCommand]
    async Task ActiverRappelsAsync()
    {
        var ok = await alertes.ActiverNotificationsAsync();
        if (auth.Current is { } u) await alertes.SynchroniserAsync(u);
        await nav.ToastAsync(ok ? "Rappels activés (J-7, J-3, J-1)" : "Autorisez les notifications dans les réglages du téléphone.");
    }
}

public partial class BourseListViewModel(AuthService auth, ConcoursService svc, AlerteService alertes, NavigationService nav) : BaseViewModel
{
    public ObservableCollection<BourseCardVm> Bourses { get; } = new();
    public ObservableCollection<ChipVm> Chips { get; } = new();

    [ObservableProperty] string avatar = "";
    [ObservableProperty] int nbNonLus;
    [ObservableProperty] string recherche = "";
    [ObservableProperty] bool vide;
    string _categorie = "Toutes";
    CancellationTokenSource? _cts;

    partial void OnRechercheChanged(string value) => _ = RechercheDiffereeAsync();

    async Task RechercheDiffereeAsync()
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        try { await Task.Delay(300, cts.Token); await ChargerListeAsync(); }
        catch (TaskCanceledException) { }
    }

    [RelayCommand]
    async Task ChargerAsync()
    {
        if (auth.Current is not { } u) return;
        Avatar = u.Initiales;
        NbNonLus = await alertes.NbNonLusAsync(u.Id);
        if (Chips.Count == 0)
        {
            Chips.Add(new ChipVm("Toutes", true));
            foreach (var c in await svc.CategoriesBoursesAsync()) Chips.Add(new ChipVm(c, false));
        }
        await ChargerListeAsync();
    }

    async Task ChargerListeAsync()
    {
        if (auth.Current is not { } u) return;
        var liste = await svc.ListerBoursesAsync(Recherche, _categorie, true);
        var favoris = await svc.FavorisAsync(u.Id, TypeCible.Bourse);
        Bourses.Clear();
        foreach (var b in liste) Bourses.Add(new BourseCardVm(b, favoris.Contains(b.Id)));
        Vide = Bourses.Count == 0;
    }

    [RelayCommand]
    async Task ChoisirCategorieAsync(ChipVm chip)
    {
        foreach (var c in Chips) c.EstSelectionne = c == chip;
        _categorie = chip.Label;
        await ChargerListeAsync();
    }

    [RelayCommand]
    async Task BasculerFavoriAsync(BourseCardVm card)
    {
        if (auth.Current is not { } u) return;
        card.EstFavori = await svc.ToggleFavoriAsync(u.Id, TypeCible.Bourse, card.Id);
        if (card.EstFavori) { await alertes.SynchroniserAsync(u); await nav.ToastAsync("Bourse enregistrée : rappels activés"); }
    }

    [RelayCommand] Task ConsulterAsync(BourseCardVm card) => nav.OuvrirLienOfficielAsync(card.B.LienOfficiel);
}
