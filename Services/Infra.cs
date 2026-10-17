using CommunityToolkit.Maui.Alerts;

namespace ConcoursCM.Services;

/// <summary>Paramètres de l'appareil (thème, économie de données, seuils de rappel).</summary>
public class SettingsService
{
    public bool LowData
    {
        get => Preferences.Get(nameof(LowData), true);
        set => Preferences.Set(nameof(LowData), value);
    }

    /// <summary>0 = clair, 1 = sombre.</summary>
    public int ThemeIndex
    {
        get => Preferences.Get(nameof(ThemeIndex), 0);
        set { Preferences.Set(nameof(ThemeIndex), value); AppliquerTheme(); }
    }

    public bool Rappel7 { get => Preferences.Get(nameof(Rappel7), true); set => Preferences.Set(nameof(Rappel7), value); }
    public bool Rappel3 { get => Preferences.Get(nameof(Rappel3), true); set => Preferences.Set(nameof(Rappel3), value); }
    public bool Rappel1 { get => Preferences.Get(nameof(Rappel1), true); set => Preferences.Set(nameof(Rappel1), value); }

    /// <summary>Seuils actifs, du plus proche au plus lointain (ex. [1, 3, 7]).</summary>
    public List<int> Seuils()
    {
        var l = new List<int>();
        if (Rappel1) l.Add(1);
        if (Rappel3) l.Add(3);
        if (Rappel7) l.Add(7);
        return l;
    }

    public void AppliquerTheme()
    {
        if (Application.Current is { } app)
            app.UserAppTheme = ThemeIndex == 1 ? AppTheme.Dark : AppTheme.Light;
    }
}

/// <summary>Navigation et boîtes de dialogue, injectables dans les ViewModels.</summary>
public class NavigationService
{
    public Task GoToAsync(string route) => Shell.Current.GoToAsync(route);
    public Task RetourAsync() => Shell.Current.GoToAsync("..");

    public Task AlertAsync(string titre, string message, string ok = "OK") =>
        Shell.Current.DisplayAlert(titre, message, ok);

    public Task<bool> ConfirmAsync(string titre, string message, string oui = "Continuer", string non = "Annuler") =>
        Shell.Current.DisplayAlert(titre, message, oui, non);

    public Task<string?> ActionSheetAsync(string titre, string annuler, params string[] options) =>
        Shell.Current.DisplayActionSheet(titre, annuler, null, options);

    public Task<string?> PromptAsync(string titre, string message, string initial = "", string placeholder = "", Keyboard? clavier = null) =>
        Shell.Current.DisplayPromptAsync(titre, message, "Valider", "Annuler", placeholder, -1, clavier ?? Keyboard.Default, initial);

    public Task ToastAsync(string message) => Toast.Make(message).Show();

    /// <summary>Ouvre un portail officiel dans le navigateur, après avertissement anti-arnaque.</summary>
    public async Task OuvrirLienOfficielAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            await AlertAsync("Lien indisponible", "Le lien officiel de cette offre n'est pas valide.");
            return;
        }
        var ok = await ConfirmAsync("Portail officiel",
            $"Vous allez quitter ConcoursCM pour ouvrir {uri.Host}.\n\nNe payez jamais de frais via un intermédiaire : uniquement sur le portail officiel.",
            "Ouvrir");
        if (ok) await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
    }
}

/// <summary>Transmet une recherche saisie sur l'accueil vers l'onglet Concours.</summary>
public class SearchContext
{
    public string? Pending { get; set; }
}

/// <summary>
/// Les SMS réels nécessitent une passerelle serveur (opérateur / agrégateur SMS).
/// Cette interface isole ce besoin : brancher l'API réelle ici, sans toucher au reste.
/// </summary>
public interface ISmsGateway
{
    Task<bool> EnvoyerAsync(string numero, string message);
}

public class PlaceholderSmsGateway : ISmsGateway
{
    public Task<bool> EnvoyerAsync(string numero, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[SMS simulé] {numero} : {message}");
        return Task.FromResult(false); // false = non envoyé (pas de passerelle)
    }
}
