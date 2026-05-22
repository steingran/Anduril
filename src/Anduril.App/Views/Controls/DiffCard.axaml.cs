using System.Windows.Input;
using Anduril.App.Models;
using Avalonia;
using Avalonia.Controls;

namespace Anduril.App.Views.Controls;

/// <summary>
/// Diff preview card that shows a file path, action kind badge, and a list of diff lines.
/// </summary>
public partial class DiffCard : UserControl
{
    public static readonly StyledProperty<string> FilePathProperty =
        AvaloniaProperty.Register<DiffCard, string>(nameof(FilePath), "untitled.txt");

    public static readonly StyledProperty<StagedActionKind> KindProperty =
        AvaloniaProperty.Register<DiffCard, StagedActionKind>(nameof(Kind), StagedActionKind.Edit);

    public static readonly StyledProperty<System.Collections.IEnumerable?> DiffLinesProperty =
        AvaloniaProperty.Register<DiffCard, System.Collections.IEnumerable?>(nameof(DiffLines));

    public static readonly StyledProperty<ICommand?> AcceptCommandProperty =
        AvaloniaProperty.Register<DiffCard, ICommand?>(nameof(AcceptCommand));

    public static readonly StyledProperty<ICommand?> RejectCommandProperty =
        AvaloniaProperty.Register<DiffCard, ICommand?>(nameof(RejectCommand));

    private static readonly DirectProperty<DiffCard, string> DiffKindLabelProperty =
        AvaloniaProperty.RegisterDirect<DiffCard, string>(
            nameof(DiffKindLabel),
            diffCard => diffCard.DiffKindLabel);

    public DiffCard()
    {
        InitializeComponent();
        UpdateHeader();
    }

    public string FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    public StagedActionKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public System.Collections.IEnumerable? DiffLines
    {
        get => GetValue(DiffLinesProperty);
        set => SetValue(DiffLinesProperty, value);
    }

    public ICommand? AcceptCommand
    {
        get => GetValue(AcceptCommandProperty);
        set => SetValue(AcceptCommandProperty, value);
    }

    public ICommand? RejectCommand
    {
        get => GetValue(RejectCommandProperty);
        set => SetValue(RejectCommandProperty, value);
    }

    public string DiffKindLabel => Kind.ToString().ToUpperInvariant();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == KindProperty)
            UpdateHeader();
    }

    private void UpdateHeader()
    {
        RaisePropertyChanged(DiffKindLabelProperty, string.Empty, DiffKindLabel);
    }
}
