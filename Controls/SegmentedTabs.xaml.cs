namespace ConcoursCM.Controls;

/// <summary>Sélecteur à deux onglets (Candidat / Institution, Clair / Sombre, Concours / Bourse).</summary>
public partial class SegmentedTabs : ContentView
{
    public static readonly BindableProperty LeftTextProperty = BindableProperty.Create(nameof(LeftText), typeof(string), typeof(SegmentedTabs), "",
        propertyChanged: (b, _, _) => ((SegmentedTabs)b).Refresh());
    public static readonly BindableProperty RightTextProperty = BindableProperty.Create(nameof(RightText), typeof(string), typeof(SegmentedTabs), "",
        propertyChanged: (b, _, _) => ((SegmentedTabs)b).Refresh());
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(SegmentedTabs), 0,
        BindingMode.TwoWay, propertyChanged: (b, _, _) => ((SegmentedTabs)b).Refresh());

    public string LeftText { get => (string)GetValue(LeftTextProperty); set => SetValue(LeftTextProperty, value); }
    public string RightText { get => (string)GetValue(RightTextProperty); set => SetValue(RightTextProperty, value); }
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    public SegmentedTabs()
    {
        InitializeComponent();
        Refresh();
    }

    void Refresh()
    {
        if (LeftLabel == null) return;
        LeftLabel.Text = LeftText; RightLabel.Text = RightText;
        Appliquer(LeftBox, LeftLabel, SelectedIndex == 0);
        Appliquer(RightBox, RightLabel, SelectedIndex == 1);
    }

    static void Appliquer(Border box, Label label, bool actif)
    {
        box.BackgroundColor = actif ? Color.FromArgb("#1E3A8A") : Colors.Transparent;
        label.TextColor = actif ? Colors.White : Color.FromArgb("#1E3A8A");
    }

    void OnLeft(object? sender, TappedEventArgs e) => SelectedIndex = 0;
    void OnRight(object? sender, TappedEventArgs e) => SelectedIndex = 1;
}
