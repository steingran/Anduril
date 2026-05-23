using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Anduril.App.Models;
using Avalonia;
using Avalonia.Controls;

namespace Anduril.App.Views.Controls;

public sealed class ModelPickerItem
{
    public required bool IsProviderHeader { get; init; }
    public required string ProviderName { get; init; }
    public ModelOption? Model { get; init; }

    public bool IsModelOption => !IsProviderHeader;
    public bool IsUnavailable => Model is not null && !Model.IsAvailable;
    public string AvailabilityLabel => IsUnavailable ? "Unavailable" : string.Empty;
}

public partial class ModelPicker : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ModelPicker, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<ModelOption?> SelectedModelProperty =
        AvaloniaProperty.Register<ModelPicker, ModelOption?>(
            nameof(SelectedModel),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<ModelPicker, string>(nameof(PlaceholderText), "Select model...");

    public static readonly StyledProperty<bool> CompactProperty =
        AvaloniaProperty.Register<ModelPicker, bool>(nameof(Compact));

    public static readonly StyledProperty<ICommand?> ConfigureCommandProperty =
        AvaloniaProperty.Register<ModelPicker, ICommand?>(nameof(ConfigureCommand));

    public static readonly StyledProperty<string> ConfigureLabelProperty =
        AvaloniaProperty.Register<ModelPicker, string>(nameof(ConfigureLabel), "Manage providers");

    public static readonly DirectProperty<ModelPicker, bool> HasConfigureActionProperty =
        AvaloniaProperty.RegisterDirect<ModelPicker, bool>(
            nameof(HasConfigureAction),
            picker => picker.HasConfigureAction);

    private readonly ObservableCollection<ModelPickerItem> _pickerItems = [];

    public ModelPicker()
    {
        InitializeComponent();
        RebuildPickerItems();
        UpdateHeaderFromSelection();
        UpdateVisualState();
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ModelOption? SelectedModel
    {
        get => GetValue(SelectedModelProperty);
        set => SetValue(SelectedModelProperty, value);
    }

    public string PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public bool Compact
    {
        get => GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    public ICommand? ConfigureCommand
    {
        get => GetValue(ConfigureCommandProperty);
        set => SetValue(ConfigureCommandProperty, value);
    }

    public string ConfigureLabel
    {
        get => GetValue(ConfigureLabelProperty);
        set => SetValue(ConfigureLabelProperty, value);
    }

    public ObservableCollection<ModelPickerItem> PickerItems => _pickerItems;

    public bool HasConfigureAction => ConfigureCommand is not null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty)
        {
            RebuildPickerItems();
            UpdateHeaderFromSelection();
        }

        if (change.Property == SelectedModelProperty)
            UpdateHeaderFromSelection();

        if (change.Property == CompactProperty)
            UpdateVisualState();

        if (change.Property == ConfigureCommandProperty)
            RaisePropertyChanged(
                HasConfigureActionProperty,
                change.GetOldValue<ICommand?>() is not null,
                change.GetNewValue<ICommand?>() is not null);
    }

    private void OnTogglePopup(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ModelPopup.IsOpen = !ModelPopup.IsOpen;
    }

    private void OnSelectModel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ModelPickerItem { IsModelOption: true, Model: { } model } })
            return;

        SelectedModel = model;
        UpdateHeaderFromSelection();
        ModelPopup.IsOpen = false;
    }

    private void RebuildPickerItems()
    {
        _pickerItems.Clear();

        if (ItemsSource is null)
            return;

        var sourceModels = ItemsSource
            .OfType<ModelOption>()
            .OrderBy(item => item.ProviderId)
            .ThenBy(item => item.DisplayName)
            .ToList();

        foreach (var group in sourceModels.GroupBy(option => ParseProviderPrefix(option.ProviderId)))
        {
            _pickerItems.Add(new ModelPickerItem
            {
                IsProviderHeader = true,
                ProviderName = group.Key,
                Model = null
            });

            foreach (var model in group)
            {
                _pickerItems.Add(new ModelPickerItem
                {
                    IsProviderHeader = false,
                    ProviderName = group.Key,
                    Model = model
                });
            }
        }
    }

    private void UpdateHeaderFromSelection()
    {
        if (SelectedModelText is null)
            return;

        if (SelectedModel is null)
        {
            SelectedModelText.Classes.Set("model-picker-selected", false);
            SelectedModelText.Classes.Set("model-picker-placeholder", true);
            SelectedModelText.Text = PlaceholderText;
            return;
        }

        SelectedModelText.Text = SelectedModel.DisplayName;
        SelectedModelText.Classes.Set("model-picker-placeholder", false);
        SelectedModelText.Classes.Set("model-picker-selected", true);
    }

    private void UpdateVisualState()
    {
        if (PickerRoot is null || OpenButton is null)
            return;

        OpenButton.Padding = Compact ? new Thickness(10, 4) : new Thickness(12, 6);
    }

    private static string ParseProviderPrefix(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return "Provider";

        var split = providerId.Split("::", StringSplitOptions.None);
        return split.Length > 0 && !string.IsNullOrWhiteSpace(split[0]) ? split[0] : "Provider";
    }
}
