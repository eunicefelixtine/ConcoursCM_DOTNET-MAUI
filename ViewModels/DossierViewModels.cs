namespace ConcoursCM.ViewModels;

public partial class ConcoursDetailViewModel(AuthService auth, ConcoursService svc, ChecklistService checklist, AlerteService alertes, NavigationService nav)
    : BaseViewModel, IQueryAttributable
{
    int _id;
    Concours? _c;

    public ObservableCollection<string> Pieces { get; } = new();

    [ObservableProperty] string titre = "";
    [ObservableProperty] string sousTitre = "";
    [ObservableProperty] string tutelle = "";
    [ObservableProperty] string joursBadge = "";
    [ObservableProperty] Color joursBg = Palette.Soft;
    [ObservableProperty] Color joursFg = Palette.Primary;
    [ObservableProperty] string dateLimite = "";
    [ObservableProperty] bool dateUrgente;
    [ObservableProperty] string epreuves = "";
    [ObservableProperty] string places = "";
    [ObservableProperty] string quittance = "";
    [ObservableProperty] string arreteNom = "";
    [ObservableProperty] string arreteInfo = "";
    [ObservableProperty] string piecesTitre = "";
    [ObservableProperty] string rappelOfficiel = "";
    [ObservableProperty] string eligibiliteTexte = "";
    [ObservableProperty] bool eligibiliteVisible;
    [ObservableProperty] Color eligibiliteColor = Colors.Gray;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(FavoriGlyph))] bool estFavori;
    [ObservableProperty] bool estClos;
    [ObservableProperty] bool estOuvert = true;
    [ObservableProperty] string checklistTexte = "Ajouter à ma checklist physique";
    [ObservableProperty] string avatar = "";

    public string FavoriGlyph => EstFavori ? "★" : "☆";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var v) && int.TryParse(v?.ToString(), out var id)) _id = id;
    }

    [RelayCommand]
    async Task ChargerAsync()
    {
        if (auth.Current is not { } u) return;
        var c = _c = await svc.ObtenirConcoursAsync(_id);
        if (c == null) { await nav.AlertAsync("Introuvable", "Ce concours n'existe plus."); await nav.RetourAsync(); return; }

        Avatar = u.Initiales;
        Titre = $"{c.Sigle} {c.DateCloture.Year}".Trim();
        SousTitre = c.Intitule;
        Tutelle = c.Tutelle;
        JoursBadge = c.BadgeJours;
        (JoursBg, JoursFg) = Palette.Badge(c.Urgence);
        DateLimite = $"{Fmt.Date(c.DateCloture)} • {c.HeureCloture.Replace(':', 'h')}";
        DateUrgente = c.Urgence is NiveauUrgence.Critique or NiveauUrgence.Urgent;
        Epreuves = c.DateEpreuves is { } d ? Fmt.Date(d) : "À annoncer";
        Places = $"{c.Places} places";
        Quittance = Fmt.Fcfa(c.FraisFcfa);
        EstClos = c.EstClos;
        EstOuvert = !c.EstClos;

        ArreteNom = string.IsNullOrWhiteSpace(c.ReferenceArrete) ? "Arrêté officiel" : c.ReferenceArrete;
        ArreteInfo = c.DateArrete is { } da ? $"Signé le {Fmt.Date(da)}" : "Consultable sur le portail officiel";

        var pieces = await svc.PiecesAsync(c.Id);
        PiecesTitre = $"Dossier requis ({pieces.Count} pièces)";
        Pieces.Clear();
        foreach (var p in pieces) Pieces.Add($"{p.Libelle}");

        RappelOfficiel = $"La soumission physique ou en ligne se fait uniquement via la plateforme institutionnelle de {c.Sigle}. " +
                         "Ne payez jamais via un intermédiaire.";

        (EligibiliteTexte, EligibiliteVisible, EligibiliteColor) = ConcoursService.Evaluer(u, c) switch
        {
            Eligibilite.Eligible => ($"✓ Votre niveau ({u.Niveau}) est accepté pour ce concours.", true, Color.FromArgb("#16A34A")),
            Eligibilite.NonEligible => ($"⚠ Niveau requis : {c.Diplome}. Votre profil indique {u.Niveau}.", true, Color.FromArgb("#B45309")),
            _ => ("Renseignez votre niveau dans Profil pour connaître votre éligibilité.", u.Role == RoleUtilisateur.Candidat, Colors.Gray)
        };

        EstFavori = (await svc.FavorisAsync(u.Id, TypeCible.Concours)).Contains(c.Id);
        ChecklistTexte = await checklist.EstSuiviAsync(u.Id, c.Id) ? "Ouvrir ma checklist physique" : "Ajouter à ma checklist physique";
    }

    [RelayCommand]
    async Task BasculerFavoriAsync()
    {
        if (auth.Current is not { } u || _c == null) return;
        EstFavori = await svc.ToggleFavoriAsync(u.Id, TypeCible.Concours, _c.Id);
        if (EstFavori) { await alertes.SynchroniserAsync(u); await nav.ToastAsync("Favori ajouté : rappels J-7 / J-3 / J-1 activés"); }
    }

    [RelayCommand]
    async Task OuvrirArreteAsync()
    {
        if (_c == null) return;
        if (!string.IsNullOrEmpty(_c.ArretePath) && File.Exists(_c.ArretePath))
            await Launcher.Default.OpenAsync(new OpenFileRequest(ArreteNom, new ReadOnlyFile(_c.ArretePath)));
        else
            await nav.OuvrirLienOfficielAsync(_c.LienOfficiel); // l'arrêté se consulte sur le portail officiel
    }

    [RelayCommand]
    async Task PostulerAsync()
    {
        if (_c == null) return;
        if (_c.EstClos) { await nav.AlertAsync("Concours clos", "La date limite de dépôt est dépassée."); return; }
        await nav.OuvrirLienOfficielAsync(_c.LienOfficiel);
    }

    [RelayCommand]
    async Task AllerChecklistAsync()
    {
        if (_c == null) return;
        await nav.GoToAsync($"checklist?id={_c.Id}");
    }
}

public partial class ChecklistViewModel(AuthService auth, ConcoursService svc, ChecklistService checklist, AlerteService alertes,
    FicheRecapService fiche, NavigationService nav) : BaseViewModel, IQueryAttributable
{
    int _id;
    Concours? _c;
    List<(PieceRequise piece, ChecklistItem item)> _donnees = new();

    public ObservableCollection<PieceChecklistVm> Pieces { get; } = new();

    [ObservableProperty] string concoursTitre = "";
    [ObservableProperty] string sessionTexte = "";
    [ObservableProperty] string pretesTexte = "";
    [ObservableProperty] double progress;
    [ObservableProperty] string consigne = "";
    [ObservableProperty] bool complet;
    [ObservableProperty] bool estClos;
    [ObservableProperty] string lieu = "";
    [ObservableProperty] string dateLimite = "";
    [ObservableProperty] string heureLimite = "";
    [ObservableProperty] string avatar = "";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var v) && int.TryParse(v?.ToString(), out var id)) _id = id;
    }

    [RelayCommand]
    async Task ChargerAsync()
    {
        if (auth.Current is not { } u) return;
        var c = _c = await svc.ObtenirConcoursAsync(_id);
        if (c == null) return;
        var premiere = !await checklist.EstSuiviAsync(u.Id, c.Id);

        Avatar = u.Initiales;
        ConcoursTitre = $"Concours {c.Sigle} {c.DateCloture.Year}";
        SessionTexte = $"SESSION {c.DateCloture.Year}";
        Lieu = string.IsNullOrWhiteSpace(c.LieuDepot) ? "Voir l'arrêté officiel" : c.LieuDepot;
        DateLimite = Fmt.Date(c.DateCloture);
        HeureLimite = $"À {c.HeureCloture.Replace(':', 'h')} précises";
        EstClos = c.EstClos;

        _donnees = await checklist.ChargerAsync(u.Id, c.Id);
        Pieces.Clear();
        foreach (var (p, i) in _donnees) Pieces.Add(new PieceChecklistVm(p, i));
        Recalculer();
        if (premiere) await alertes.SynchroniserAsync(u); // suivre un concours = rappels automatiques
    }

    [RelayCommand]
    async Task BasculerAsync(PieceChecklistVm p)
    {
        if (auth.Current is not { } u) return;
        var etaitComplet = Complet;
        p.Coche = !p.Coche;
        await checklist.DefinirCocheAsync(p.Item, p.Coche);
        Recalculer();
        await alertes.SynchroniserAsync(u);
        if (Complet && !etaitComplet) await nav.ToastAsync("Dossier complet ✓ Vous pouvez le déposer");
    }

    void Recalculer()
    {
        var total = Pieces.Count;
        var faites = Pieces.Count(p => p.Coche);
        var pct = total == 0 ? 0 : (int)Math.Round(100.0 * faites / total);
        Progress = total == 0 ? 0 : (double)faites / total;
        PretesTexte = $"{faites}/{total} prêtes ({pct}%)";
        Complet = total > 0 && faites == total;

        if (_c == null) return;
        Consigne = EstClos ? "La date limite est dépassée : ce dossier ne peut plus être déposé."
            : Complet ? $"Dossier complet ✓ Déposez-le en version papier à : {Lieu}, avant le {DateLimite} à {_c.HeureCloture.Replace(':', 'h')}."
            : $"Dossier physique à déposer obligatoirement en version papier. Pièces manquantes : {string.Join(", ", Pieces.Where(p => !p.Coche).Select(p => p.Libelle))}.";
    }

    [RelayCommand]
    async Task GenererFicheAsync()
    {
        if (auth.Current is not { } u || _c == null) return;
        if (IsBusy) return;
        IsBusy = true;
        try { await fiche.GenererEtPartagerAsync(u, _c, _donnees); }
        catch (Exception ex) { await nav.AlertAsync("Fiche PDF", "Impossible de générer la fiche : " + ex.Message); }
        finally { IsBusy = false; }
    }
}

public partial class CandidaturesViewModel(AuthService auth, ChecklistService checklist, NavigationService nav) : BaseViewModel
{
    public ObservableCollection<SuiviVm> Suivis { get; } = new();
    [ObservableProperty] bool vide;
    [ObservableProperty] string avatar = "";

    [RelayCommand]
    async Task ChargerAsync()
    {
        if (auth.Current is not { } u) return;
        Avatar = u.Initiales;
        Suivis.Clear();
        foreach (var s in await checklist.SuivisAsync(u.Id)) Suivis.Add(new SuiviVm(s));
        Vide = Suivis.Count == 0;
    }

    [RelayCommand]
    async Task OuvrirAsync(SuiviVm s)
    {
        var choix = await nav.ActionSheetAsync(s.Titre, "Fermer", "Ouvrir la checklist", "Voir le concours", "Ne plus suivre");
        switch (choix)
        {
            case "Ouvrir la checklist": await nav.GoToAsync($"checklist?id={s.ConcoursId}"); break;
            case "Voir le concours": await nav.GoToAsync($"concours-detail?id={s.ConcoursId}"); break;
            case "Ne plus suivre":
                if (auth.Current is { } u && await nav.ConfirmAsync("Ne plus suivre", "Votre progression sur la checklist sera supprimée.", "Supprimer"))
                { await checklist.RetirerSuiviAsync(u.Id, s.ConcoursId); await ChargerAsync(); }
                break;
        }
    }
}
