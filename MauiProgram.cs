using CommunityToolkit.Maui;
using ConcoursCM.ViewModels;
using ConcoursCM.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;

namespace ConcoursCM;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseLocalNotification();

        var fr = new CultureInfo("fr-FR");
        CultureInfo.DefaultThreadCurrentCulture = fr;
        CultureInfo.DefaultThreadCurrentUICulture = fr;

        // ---- Services (singletons : état partagé & une seule connexion SQLite)
        var s = builder.Services;
        s.AddSingleton<ApiClient>();
        s.AddSingleton<DatabaseService>();
        s.AddSingleton<SettingsService>();
        s.AddSingleton<NavigationService>();
        s.AddSingleton<SearchContext>();
        s.AddSingleton<ISmsGateway, ApiSmsGateway>();
        s.AddSingleton<AuthService>();
        s.AddSingleton<ConcoursService>();
        s.AddSingleton<ChecklistService>();
        s.AddSingleton<AlerteService>();
        s.AddSingleton<PublicationService>();
        s.AddSingleton<FicheRecapService>();

        // ---- Shell
        s.AddSingleton<AppShell>();

        // ---- Pages + ViewModels
        AddPage<LoginPage, LoginViewModel>(s);
        AddPage<RegisterPage, RegisterViewModel>(s);
        AddPage<InstitutionRegisterPage, InstitutionRegisterViewModel>(s);
        AddPage<HomePage, HomeViewModel>(s);
        AddPage<ConcoursListPage, ConcoursListViewModel>(s);
        AddPage<BourseListPage, BourseListViewModel>(s);
        AddPage<ConcoursDetailPage, ConcoursDetailViewModel>(s);
        AddPage<ChecklistPage, ChecklistViewModel>(s);
        AddPage<ProfilPage, ProfilViewModel>(s);
        AddPage<CandidaturesPage, CandidaturesViewModel>(s);
        AddPage<AlertesPage, AlertesViewModel>(s);
        AddPage<PublishPage, PublishViewModel>(s);

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }

    static void AddPage<TPage, TVm>(IServiceCollection s) where TPage : class where TVm : class
    {
        s.AddTransient<TVm>();
        s.AddTransient<TPage>();
    }
}
