using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConcoursCM.Services;

/// <summary>
/// Client HTTP du backend ConcoursCM. Toutes les méthodes renvoient <c>null</c> quand le serveur
/// est injoignable : les services basculent alors automatiquement sur la base SQLite locale.
/// </summary>
public sealed class ApiClient : IDisposable
{
    public const string CleUrl = "api_base_url";
    public const string CleJeton = "session_jeton";
    public const string CleUrlActive = "api_base_url_active";

    // 10.0.2.2 = l'hôte depuis l'émulateur Android ; 127.0.0.1 = « adb reverse tcp:5080 tcp:5080 » ;
    // l'IP LAN = téléphone et PC sur le même réseau. Les deux dernières échouent instantanément
    // quand elles ne s'appliquent pas, donc l'ordre ne coûte qu'une fraction de seconde.
    static readonly string[] UrlEmulateur = ["http://10.0.2.2:5080", "http://127.0.0.1:5080", "http://192.168.1.162:5080"];
    static readonly string[] UrlTelephone = ["http://127.0.0.1:5080", "http://192.168.1.162:5080", "http://10.0.2.2:5080"];

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    readonly HttpClient _http;
    string? _baseUrl;
    int _indexUrl;
    bool _enLigne = true;
    DateTime _prochaineTentative = DateTime.MinValue;

    public ApiClient()
    {
        _http = new HttpClient(new SocketsHttpHandler
        {
            // Une IP morte (10.0.2.2 depuis un vrai téléphone) doit être rejetée en ~1 s,
            // pas après le délai global : c'était la cause des blocages de plusieurs secondes.
            ConnectTimeout = TimeSpan.FromSeconds(1),
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        { Timeout = TimeSpan.FromSeconds(4) };
    }

    public string UrlParDefaut => Candidates[0];

    static string[] Candidates => EstEmulateur() ? UrlEmulateur : UrlTelephone;

    static bool EstEmulateur()
    {
        try
        {
            var m = $"{DeviceInfo.Model} {DeviceInfo.Manufacturer} {DeviceInfo.Name}";
            return m.Contains("emulator", StringComparison.OrdinalIgnoreCase)
                || m.Contains("ranchu", StringComparison.OrdinalIgnoreCase)
                || m.Contains("sdk_gphone", StringComparison.OrdinalIgnoreCase)
                || m.Contains("genymotion", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static string Jeton => Preferences.Get(CleJeton, "");

    public static void DefinirJeton(string? jeton)
    {
        if (string.IsNullOrEmpty(jeton)) Preferences.Remove(CleJeton);
        else Preferences.Set(CleJeton, jeton);
    }

    /// <summary>URL effective du backend. Forçage manuel via <see cref="CleUrl"/>, sinon la dernière connue.</summary>
    public string BaseUrl
    {
        get
        {
            if (_baseUrl != null) return _baseUrl;

            var forcee = Preferences.Get(CleUrl, "");
            if (!string.IsNullOrWhiteSpace(forcee))
                return _baseUrl = forcee.TrimEnd('/') + "/";

            var connue = Preferences.Get(CleUrlActive, "");
            if (!string.IsNullOrWhiteSpace(connue) && Array.IndexOf(Candidates, connue) >= 0)
            {
                _indexUrl = Array.IndexOf(Candidates, connue);
                return _baseUrl = connue + "/";
            }

            _indexUrl = 0;
            return _baseUrl = Candidates[0] + "/";
        }
    }

    /// <summary>L'URL courante d'abord, puis les autres candidates. Une seule URL si un forçage est actif.</summary>
    IEnumerable<string> UrlsAEssayer()
    {
        if (!string.IsNullOrWhiteSpace(Preferences.Get(CleUrl, ""))) { yield return BaseUrl; yield break; }

        var courante = BaseUrl.TrimEnd('/');
        yield return courante;
        for (var i = 0; i < Candidates.Length; i++)
            if (Candidates[i] != courante)
                yield return Candidates[i];
    }

    async Task<HttpResponseMessage?> EnvoyerAsync(HttpMethod methode, string chemin, object? corps)
    {
        if (!_enLigne && DateTime.UtcNow < _prochaineTentative) return null;

        foreach (var url in UrlsAEssayer())
        {
            var reponse = await EnvoyerSurAsync(url, methode, chemin, corps);
            if (reponse == null) continue;   // échec réseau : on essaie l'URL suivante

            _baseUrl = url;
            _indexUrl = Array.IndexOf(Candidates, url);
            _enLigne = true;
            Preferences.Set(CleUrlActive, url.TrimEnd('/'));
            return reponse;
        }

        // Aucune URL n'a répondu : on laisse respirer l'app (repli SQLite immédiat ci-dessous).
        _enLigne = false;
        _prochaineTentative = DateTime.UtcNow.AddSeconds(30);
        Debug.WriteLine($"[API] {chemin} : aucune URL joignable, bascule hors ligne 30 s");
        return null;
    }

    /// <summary>Envoie sur une URL précise. Renvoie <c>null</c> uniquement sur échec réseau.</summary>
    async Task<HttpResponseMessage?> EnvoyerSurAsync(string url, HttpMethod methode, string chemin, object? corps)
    {
        try
        {
            using var delai = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            using var requete = new HttpRequestMessage(methode, url.TrimEnd('/') + "/" + chemin.TrimStart('/'));
            if (corps != null) requete.Content = JsonContent.Create(corps, options: Json);
            // Accept-Encoding est ajouté automatiquement par SocketsHttpHandler grâce à
            // AutomaticDecompression, qui décompresse aussi la réponse.
            var jeton = Jeton;
            if (!string.IsNullOrEmpty(jeton)) requete.Headers.TryAddWithoutValidation("X-Jeton", jeton);

            return await _http.SendAsync(requete, delai.Token);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[API] {methode.Method} {chemin} ({url}) : {ex.Message}");
            return null;
        }
    }

    public async Task<T?> GetAsync<T>(string chemin)
    {
        using var reponse = await EnvoyerAsync(HttpMethod.Get, chemin, null);
        return reponse is { IsSuccessStatusCode: true } ? await LireAsync<T>(reponse) : default;
    }

    public async Task<T?> PostAsync<T>(string chemin, object? corps = null)
    {
        using var reponse = await EnvoyerAsync(HttpMethod.Post, chemin, corps);
        return reponse is { IsSuccessStatusCode: true } ? await LireAsync<T>(reponse) : default;
    }

    public async Task<T?> PutAsync<T>(string chemin, object? corps = null)
    {
        using var reponse = await EnvoyerAsync(HttpMethod.Put, chemin, corps);
        return reponse is { IsSuccessStatusCode: true } ? await LireAsync<T>(reponse) : default;
    }

    public async Task<bool> DeleteAsync(string chemin)
    {
        using var reponse = await EnvoyerAsync(HttpMethod.Delete, chemin, null);
        return reponse is { IsSuccessStatusCode: true };
    }

    static async Task<T?> LireAsync<T>(HttpResponseMessage reponse)
    {
        try { return await reponse.Content.ReadFromJsonAsync<T>(Json); }
        catch (Exception ex)
        {
            Debug.WriteLine($"[API] Lecture JSON : {ex.Message}");
            return default;
        }
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>Passerelle SMS branchée sur le backend (boîte d'envoi serveur).</summary>
public sealed class ApiSmsGateway(ApiClient api) : ISmsGateway
{
    public async Task<bool> EnvoyerAsync(string numero, string message)
    {
        var reponse = await api.PostAsync<SmsApiResponse>("/api/sms", new SmsApiRequest(numero, message));
        if (reponse != null) return reponse.Envoye;
        System.Diagnostics.Debug.WriteLine($"[SMS simulé hors ligne] {numero} : {message}");
        return false;
    }
}
