namespace ConcoursCM.Controls;

/// <summary>Barre supérieure commune : logo ou retour, titre, cloche avec compteur, avatar.</summary>
public partial class TopBar : ContentView
{
    static BindableProperty Prop<T>(string name, T def) =>
        BindableProperty.Create(name, typeof(T), typeof(TopBar), def, propertyChanged: (b, _, _) => ((TopBar)b).Refresh());

    public static readonly BindableProperty TitleProperty = Prop(nameof(Title), "");
    public static readonly BindableProperty ShowBackProperty = Prop(nameof(ShowBack), false);
    public static readonly BindableProperty ShowBellProperty = Prop(nameof(ShowBell), true);
    public static readonly BindableProperty BadgeCountProperty = Prop(nameof(BadgeCount), 0);
    public static readonly BindableProperty AvatarTextProperty = Prop(nameof(AvatarText), "");

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool ShowBack { get => (bool)GetValue(ShowBackProperty); set => SetValue(ShowBackProperty, value); }
    public bool ShowBell { get => (bool)GetValue(ShowBellProperty); set => SetValue(ShowBellProperty, value); }
    public int BadgeCount { get => (int)GetValue(BadgeCountProperty); set => SetValue(BadgeCountProperty, value); }
    public string AvatarText { get => (string)GetValue(AvatarTextProperty); set => SetValue(AvatarTextProperty, value); }

    public TopBar()
    {
        InitializeComponent();
        Refresh();
    }

    void Refresh()
    {
        if (TitleLabel == null) return;
        TitleLabel.Text = Title;
        BackBox.IsVisible = ShowBack; LogoBox.IsVisible = !ShowBack;
        BrandLabel.IsVisible = !ShowBack;
        BellBox.IsVisible = ShowBell;
        BadgeBox.IsVisible = BadgeCount > 0;
        BadgeLabel.Text = BadgeCount > 9 ? "9+" : BadgeCount.ToString();
        AvatarLabel.Text = string.IsNullOrEmpty(AvatarText) ? "?" : AvatarText;
    }

    async void OnBack(object? s, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
    async void OnBell(object? s, TappedEventArgs e) => await Shell.Current.GoToAsync("alertes");
    async void OnAvatar(object? s, TappedEventArgs e) => await Shell.Current.GoToAsync("//main/profil");
}
