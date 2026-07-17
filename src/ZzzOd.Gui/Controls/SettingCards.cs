using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using FluentAvalonia.UI.Controls;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.Controls;

public class ZzzSettingCard : SettingsExpanderItem
{
    private readonly Control _settingContent;
    private string _description;
    private string _statusText = string.Empty;

    public ZzzSettingCard(string title, string? description, Control content)
    {
        Title = title;
        Content = title;
        _description = description ?? string.Empty;
        base.Description = _description;
        _settingContent = content;
        Footer = content;
    }

    public string Title { get; }

    public new string Description => _description;

    public ZzzSettingCardStatus Status { get; private set; } = ZzzSettingCardStatus.Normal;

    public string StatusText => _statusText;

    public Control SettingContent => _settingContent;

    public void SetDescription(string? description)
    {
        _description = description ?? string.Empty;
        UpdateDescription();
    }

    public void SetDisabled(bool disabled, string? reason = null)
    {
        IsEnabled = !disabled;
        SetStatus(disabled ? ZzzSettingCardStatus.Disabled : ZzzSettingCardStatus.Normal, reason);
    }

    public void SetWaiting(bool waiting, string? message = null)
    {
        _settingContent.IsEnabled = !waiting;
        SetStatus(waiting ? ZzzSettingCardStatus.Waiting : ZzzSettingCardStatus.Normal, message);
    }

    public void SetError(string? message) =>
        SetStatus(string.IsNullOrWhiteSpace(message) ? ZzzSettingCardStatus.Normal : ZzzSettingCardStatus.Error, message);

    public void SetValidation(string? message) =>
        SetStatus(string.IsNullOrWhiteSpace(message) ? ZzzSettingCardStatus.Normal : ZzzSettingCardStatus.Validation, message);

    public void ClearStatus()
    {
        _settingContent.IsEnabled = true;
        SetStatus(ZzzSettingCardStatus.Normal, null);
    }

    private void SetStatus(ZzzSettingCardStatus status, string? message)
    {
        Status = status;
        _statusText = message ?? string.Empty;
        UpdateDescription();
    }

    private void UpdateDescription()
    {
        base.Description = string.IsNullOrWhiteSpace(_statusText)
            ? _description
            : string.IsNullOrWhiteSpace(_description)
                ? _statusText
                : $"{_description}{Environment.NewLine}{_statusText}";
    }
}

public enum ZzzSettingCardStatus
{
    Normal,

    Disabled,

    Waiting,

    Error,

    Validation,
}

public sealed class ZzzSwitchSettingCard : ZzzSettingCard
{
    private readonly ToggleSwitch _switch;
    private readonly IZzzConfigBinding<bool>? _binding;

    public ZzzSwitchSettingCard(string title, string? description = null, IZzzConfigBinding<bool>? binding = null)
        : base(title, description, CreateSwitch(out ToggleSwitch toggleSwitch))
    {
        _switch = toggleSwitch;
        _binding = binding;
        _switch.IsChecked = binding?.Read() ?? false;
        _switch.IsCheckedChanged += (_, _) =>
        {
            if (_binding is not null)
            {
                _binding.Save(_switch.IsChecked == true);
            }
        };
    }

    private static ToggleSwitch CreateSwitch(out ToggleSwitch toggleSwitch)
    {
        toggleSwitch = new ToggleSwitch();
        return toggleSwitch;
    }
}

public class ZzzComboBoxSettingCard<T> : ZzzSettingCard
{
    private readonly FAComboBox _comboBox;
    private readonly IZzzConfigBinding<T>? _binding;

    public event EventHandler? SelectionChanged;

    public T? SelectedValue => _comboBox.SelectedItem is ZzzConfigOption<T> option ? option.Value : default;

    public IReadOnlyList<ZzzConfigOption<T>> Options =>
        (_comboBox.ItemsSource as IEnumerable<ZzzConfigOption<T>>)?.ToArray() ?? [];

    public ZzzComboBoxSettingCard(string title, IEnumerable<ZzzConfigOption<T>> options, string? description = null, IZzzConfigBinding<T>? binding = null, bool editable = false)
        : base(title, description, CreateComboBox(options, editable, out FAComboBox comboBox))
    {
        _comboBox = comboBox;
        _binding = binding;
        if (binding is not null)
        {
            T current = binding.Read();
            _comboBox.SelectedItem = options.FirstOrDefault(option => EqualityComparer<T>.Default.Equals(option.Value, current));
        }

        _comboBox.SelectionChanged += (_, _) =>
        {
            if (_binding is not null && _comboBox.SelectedItem is ZzzConfigOption<T> option)
            {
                _binding.Save(option.Value);
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public void ReloadOptions(IEnumerable<ZzzConfigOption<T>> options)
    {
        ZzzConfigOption<T>[] values = options.ToArray();
        _comboBox.ItemsSource = values;
        if (_binding is null)
        {
            _comboBox.SelectedIndex = values.Length > 0 ? 0 : -1;
            return;
        }

        T current = _binding.Read();
        _comboBox.SelectedItem = values.FirstOrDefault(option => EqualityComparer<T>.Default.Equals(option.Value, current));
    }

    private static FAComboBox CreateComboBox(IEnumerable<ZzzConfigOption<T>> options, bool editable, out FAComboBox comboBox)
    {
        comboBox = new FAComboBox
        {
            MinWidth = editable ? 220 : 180,
            ItemsSource = options.ToArray(),
        };
        return comboBox;
    }
}

public sealed class ZzzEditableComboBoxSettingCard : ZzzSettingCard
{
    private readonly TextBox _textBox;
    private readonly IZzzConfigBinding<string>? _binding;

    public ZzzEditableComboBoxSettingCard(string title, IEnumerable<string> options, string? description = null, IZzzConfigBinding<string>? binding = null)
        : base(title, description, CreateEditor(options, binding, out TextBox textBox))
    {
        _textBox = textBox;
        _binding = binding;
        _textBox.LostFocus += (_, _) => _binding?.Save(_textBox.Text ?? string.Empty);
    }

    private static StackPanel CreateEditor(IEnumerable<string> options, IZzzConfigBinding<string>? binding, out TextBox textBox)
    {
        TextBox editor = new()
        {
            Text = binding?.Read() ?? string.Empty,
            MinWidth = 180,
        };
        textBox = editor;
        FAComboBox comboBox = new()
        {
            ItemsSource = options.ToArray(),
            MinWidth = 160,
        };
        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is string value)
            {
                editor.Text = value;
                binding?.Save(value);
            }
        };
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { comboBox, textBox },
        };
    }
}

public class ZzzNumberSettingCard : ZzzSettingCard
{
    private readonly NumberBox _number;
    private readonly IZzzConfigBinding<double>? _binding;

    public ZzzNumberSettingCard(string title, double minimum, double maximum, string? description = null, IZzzConfigBinding<double>? binding = null)
        : base(title, description, CreateNumber(minimum, maximum, binding, out NumberBox number))
    {
        _number = number;
        _binding = binding;
        _number.ValueChanged += (_, _) =>
        {
            if (_binding is not null)
            {
                _binding.Save(_number.Value);
            }
        };
    }

    private static NumberBox CreateNumber(double minimum, double maximum, IZzzConfigBinding<double>? binding, out NumberBox number)
    {
        number = new NumberBox
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = binding?.Read() ?? minimum,
            MinWidth = 140,
        };
        return number;
    }
}

public sealed class ZzzSpinBoxSettingCard : ZzzNumberSettingCard
{
    public ZzzSpinBoxSettingCard(string title, int minimum, int maximum, string? description = null, IZzzConfigBinding<double>? binding = null)
        : base(title, minimum, maximum, description, binding)
    {
    }
}

public sealed class ZzzDoubleSpinBoxSettingCard : ZzzNumberSettingCard
{
    public ZzzDoubleSpinBoxSettingCard(string title, double minimum, double maximum, string? description = null, IZzzConfigBinding<double>? binding = null)
        : base(title, minimum, maximum, description, binding)
    {
    }
}

public sealed class ZzzTextSettingCard : ZzzSettingCard
{
    private readonly TextBox _textBox;
    private readonly IZzzConfigBinding<string>? _binding;

    public ZzzTextSettingCard(string title, string? description = null, IZzzConfigBinding<string>? binding = null, bool password = false)
        : base(title, description, CreateTextBox(binding, password, out TextBox textBox))
    {
        _textBox = textBox;
        _binding = binding;
        _textBox.LostFocus += (_, _) =>
        {
            _binding?.Save(_textBox.Text ?? string.Empty);
        };
    }

    private static TextBox CreateTextBox(IZzzConfigBinding<string>? binding, bool password, out TextBox textBox)
    {
        textBox = new TextBox
        {
            Text = binding?.Read() ?? string.Empty,
            MinWidth = 220,
        };
        if (password)
        {
            textBox.PasswordChar = '*';
        }

        return textBox;
    }
}

public sealed class ZzzPushSettingCard : ZzzSettingCard
{
    public ZzzPushSettingCard(string title, string buttonText, Action clicked, string? description = null)
        : base(title, description, CreateButton(buttonText, clicked))
    {
    }

    private static CommandBarButton CreateButton(string buttonText, Action clicked)
    {
        CommandBarButton button = new() { Label = buttonText };
        button.Click += (_, _) => clicked();
        return button;
    }
}

public sealed class ZzzMultiPushSettingCard : ZzzSettingCard
{
    public ZzzMultiPushSettingCard(string title, IEnumerable<(string Text, Action Clicked)> buttons, string? description = null)
        : base(title, description, CreateButtons(buttons))
    {
    }

    private static CommandBar CreateButtons(IEnumerable<(string Text, Action Clicked)> buttons)
    {
        CommandBar commandBar = new();
        foreach ((string text, Action clicked) in buttons)
        {
            CommandBarButton button = new() { Label = text };
            button.Click += (_, _) => clicked();
            commandBar.PrimaryCommands.Add(button);
        }

        return commandBar;
    }
}

public sealed class ZzzKeyCaptureSettingCard : ZzzSettingCard
{
    private readonly TextBox _keyBox;
    private readonly IZzzConfigBinding<string>? _binding;

    public ZzzKeyCaptureSettingCard(string title, string? description = null, IZzzConfigBinding<string>? binding = null)
        : base(title, description, CreateKeyBox(binding, out TextBox keyBox))
    {
        _keyBox = keyBox;
        _binding = binding;
        _keyBox.KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        string key = e.Key.ToString();
        _keyBox.Text = key;
        _binding?.Save(key);
        e.Handled = true;
    }

    private static TextBox CreateKeyBox(IZzzConfigBinding<string>? binding, out TextBox keyBox)
    {
        keyBox = new TextBox
        {
            Text = binding?.Read() ?? string.Empty,
            Watermark = "按下按键",
            MinWidth = 160,
        };
        return keyBox;
    }
}
