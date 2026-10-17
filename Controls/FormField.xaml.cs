namespace ConcoursCM.Controls;

/// <summary>Champ de saisie réutilisable : libellé, icône, indication, œil pour mot de passe, message d'erreur.</summary>
public partial class FormField : ContentView
{
    bool _motDePasseVisible;

    public static readonly BindableProperty LabelTextProperty = Prop<string>(nameof(LabelText), "");
    public static readonly BindableProperty HintProperty = Prop<string>(nameof(Hint), "");
    public static readonly BindableProperty PlaceholderProperty = Prop<string>(nameof(Placeholder), "");
    public static readonly BindableProperty IconProperty = Prop<string>(nameof(Icon), "");
    public static readonly BindableProperty IsPasswordProperty = Prop<bool>(nameof(IsPassword), false);
    public static readonly BindableProperty ErrorTextProperty = Prop<string>(nameof(ErrorText), "");
    public static readonly BindableProperty KeyboardTypeProperty = Prop<Keyboard>(nameof(KeyboardType), Keyboard.Default);
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(FormField), "",
        BindingMode.TwoWay, propertyChanged: (b, _, _) => ((FormField)b).Refresh());

    static BindableProperty Prop<T>(string name, T def) =>
        BindableProperty.Create(name, typeof(T), typeof(FormField), def, propertyChanged: (b, _, _) => ((FormField)b).Refresh());

    public string LabelText { get => (string)GetValue(LabelTextProperty); set => SetValue(LabelTextProperty, value); }
    public string Hint { get => (string)GetValue(HintProperty); set => SetValue(HintProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public bool IsPassword { get => (bool)GetValue(IsPasswordProperty); set => SetValue(IsPasswordProperty, value); }
    public string ErrorText { get => (string)GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
    public Keyboard KeyboardType { get => (Keyboard)GetValue(KeyboardTypeProperty); set => SetValue(KeyboardTypeProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public FormField()
    {
        InitializeComponent();
        Refresh();
    }

    void Refresh()
    {
        if (Input == null) return;
        TitleLabel.Text = LabelText; TitleLabel.IsVisible = !string.IsNullOrEmpty(LabelText);
        HintLabel.Text = Hint;
        IconLabel.Text = Icon; IconLabel.IsVisible = !string.IsNullOrEmpty(Icon);
        Input.Placeholder = Placeholder;
        Input.Keyboard = KeyboardType ?? Keyboard.Default;
        Input.IsPassword = IsPassword && !_motDePasseVisible;
        EyeLabel.IsVisible = IsPassword;
        ErrorLabel.Text = ErrorText; ErrorLabel.IsVisible = !string.IsNullOrEmpty(ErrorText);
        if ((Input.Text ?? "") != (Text ?? "")) Input.Text = Text ?? "";
    }

    void OnTextChanged(object? sender, TextChangedEventArgs e) => Text = e.NewTextValue ?? "";

    void OnEyeTapped(object? sender, TappedEventArgs e)
    {
        _motDePasseVisible = !_motDePasseVisible;
        EyeLabel.Text = _motDePasseVisible ? "🙈" : "👁";
        Refresh();
    }
}
