using Avalonia;
using Avalonia.Controls;

namespace Anduril.App.Views.Controls;

public partial class AndurilCard : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AndurilCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<AndurilCard, string?>(nameof(Subtitle));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<AndurilCard, object?>(nameof(Footer));

    public static readonly StyledProperty<object?> BodyProperty =
        AvaloniaProperty.Register<AndurilCard, object?>(nameof(Body));

    public static readonly StyledProperty<bool> IsEmphasisProperty =
        AvaloniaProperty.Register<AndurilCard, bool>(nameof(IsEmphasis));

    public AndurilCard()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public bool IsEmphasis
    {
        get => GetValue(IsEmphasisProperty);
        set => SetValue(IsEmphasisProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleProperty ||
            change.Property == SubtitleProperty ||
            change.Property == BodyProperty ||
            change.Property == FooterProperty ||
            change.Property == IsEmphasisProperty)
        {
            UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        if (CardRoot is null)
            return;

        HeaderPanel.IsVisible = !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Subtitle);
        TitleBlock.IsVisible = !string.IsNullOrWhiteSpace(Title);
        SubtitleBlock.IsVisible = !string.IsNullOrWhiteSpace(Subtitle);
        FooterPresenter.IsVisible = Footer is not null;
        CardRoot.Classes.Set("emphasis", IsEmphasis);
    }
}
