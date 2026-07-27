using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Notifications;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.PageModels.Settings;

internal sealed record ZzzPushOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzPushFieldModel : INotifyPropertyChanged
{
    private string _value = string.Empty;
    private ZzzPushOption? _selectedOption;

    public required ZzzPushFieldDescriptor Descriptor { get; init; }

    public string DisplayTitle => Descriptor.Required ? $"{Descriptor.Title} *" : Descriptor.Title;

    public string Placeholder => Descriptor.Placeholder;

    public bool IsCombo => Descriptor.FieldType == ZzzPushFieldType.Combo;

    public bool IsText => !IsCombo;

    public bool AcceptsReturn => Descriptor.FieldType is ZzzPushFieldType.KeyValue or ZzzPushFieldType.CodeEditor;

    public IReadOnlyList<ZzzPushOption> Options { get; init; } = [];

    public string Value
    {
        get => _value;
        set => SetField(ref _value, value ?? string.Empty);
    }

    public ZzzPushOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (SetField(ref _selectedOption, value) && value is not null)
            {
                Value = value.Value;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

