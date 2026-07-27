using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Notifications;
using ZzzOd.Gui.Architecture;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.Views.FrontierPages.Settings;

internal sealed record ZzzPushOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed partial class ZzzPushFieldModel : ObservableObject
{
    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private ZzzPushOption? _selectedOption;

    public required ZzzPushFieldDescriptor Descriptor { get; init; }

    public string DisplayTitle => Descriptor.Required ? $"{Descriptor.Title} *" : Descriptor.Title;

    public string Placeholder => Descriptor.Placeholder;

    public bool IsCombo => Descriptor.FieldType == ZzzPushFieldType.Combo;

    public bool IsText => !IsCombo;

    public bool AcceptsReturn => Descriptor.FieldType is ZzzPushFieldType.KeyValue or ZzzPushFieldType.CodeEditor;

    public IReadOnlyList<ZzzPushOption> Options { get; init; } = [];

    partial void OnSelectedOptionChanged(ZzzPushOption? value)
    {
        if (value is not null)
        {
            Value = value.Value;
        }
    }
}

internal sealed class ZzzPushSettingsViewModel : ZzzPageViewModel
{
    private readonly NotifySection _notify;
    private readonly PushSection _push;
    private readonly EnvironmentSection _environment;
    private readonly Action<string?>? _errorReporter;
    private string? _lastError;
    private bool _loadingSections;

    public ZzzPushSettingsViewModel(
        IZzzAppBackend backend,
        IZzzPushNotificationService pushService,
        Action<string?>? errorReporter = null)
    {
        _errorReporter = errorReporter;
        _notify = new NotifySection(backend, OnSectionError);
        _push = new PushSection(backend, pushService.Channels, OnSectionError);
        _environment = new EnvironmentSection(backend, OnSectionError);
        ProxyOptions = [new("不启用", "NONE"), new("个人代理", "PERSONAL")];
    }

    public IReadOnlyList<ZzzPushOption> ProxyOptions { get; }

    public string? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    public string Title
    {
        get => _notify.Title;
        set => _notify.Title = value;
    }

    public bool SendImage
    {
        get => _push.SendImage;
        set => _push.SendImage = value;
    }

    public ZzzPushOption? SelectedProxy
    {
        get => ProxyOptions.FirstOrDefault(option => string.Equals(option.Value, _push.Proxy, StringComparison.Ordinal));
        set
        {
            if (value is null || string.Equals(_push.Proxy, value.Value, StringComparison.Ordinal))
            {
                return;
            }

            _push.Proxy = value.Value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PersonalProxyVisible));
        }
    }

    public string PersonalProxy
    {
        get => _environment.PersonalProxy;
        set => _environment.PersonalProxy = value;
    }

    public bool PersonalProxyVisible => string.Equals(_push.Proxy, "PERSONAL", StringComparison.Ordinal);

    public override void OnPageShown()
    {
        base.OnPageShown();
        _loadingSections = true;
        try
        {
            _notify.OnPageShown();
            _push.OnPageShown();
            _environment.OnPageShown();
        }
        finally
        {
            _loadingSections = false;
        }

        ReportError(_notify.LastError ?? _push.LastError ?? _environment.LastError);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SendImage));
        OnPropertyChanged(nameof(SelectedProxy));
        OnPropertyChanged(nameof(PersonalProxy));
        OnPropertyChanged(nameof(PersonalProxyVisible));
    }

    public object? GetPushValue(string key, string defaultValue) => _push.GetFieldValue(key, defaultValue);

    public bool SaveValue(string scope, string key, object? value) => scope switch
    {
        "notify" => _notify.SaveFieldValue(key, value),
        "push" => _push.SaveFieldValue(key, value),
        "env" => _environment.SaveFieldValue(key, value),
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "未知通知配置 scope。"),
    };

    protected override void DisposePageCore()
    {
        _notify.DisposePage();
        _push.DisposePage();
        _environment.DisposePage();
    }

    private void OnSectionError(string? error)
    {
        if (!_loadingSections)
        {
            ReportError(error);
        }
    }

    private void ReportError(string? error)
    {
        LastError = error;
        _errorReporter?.Invoke(error);
    }

    private sealed class NotifySection : ZzzConfigSectionViewModel
    {
        private static readonly ZzzConfigField TitleField = Text("title", "一条龙运行通知");
        private static readonly IReadOnlyList<ZzzConfigField> FieldList = [TitleField];

        public NotifySection(IZzzAppBackend backend, Action<string?> errorReporter) : base(backend, errorReporter)
        {
        }

        protected override string ScopeName => "notify";
        protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

        public string Title { get => GetValue<string>(TitleField); set => SetValue(TitleField, value); }

        public bool SaveFieldValue(string key, object? value)
        {
            if (!string.Equals(key, TitleField.Key, StringComparison.Ordinal))
            {
                throw new ArgumentOutOfRangeException(nameof(key), key, "未知通知标题配置项。");
            }

            return SaveValue(TitleField, value);
        }
    }

    private sealed class EnvironmentSection : ZzzConfigSectionViewModel
    {
        private static readonly ZzzConfigField PersonalProxyField = Text("personal_proxy", string.Empty);
        private static readonly IReadOnlyList<ZzzConfigField> FieldList = [PersonalProxyField];

        public EnvironmentSection(IZzzAppBackend backend, Action<string?> errorReporter) : base(backend, errorReporter)
        {
        }

        protected override string ScopeName => "env";
        protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

        public string PersonalProxy { get => GetValue<string>(PersonalProxyField); set => SetValue(PersonalProxyField, value); }

        public bool SaveFieldValue(string key, object? value)
        {
            if (!string.Equals(key, PersonalProxyField.Key, StringComparison.Ordinal))
            {
                throw new ArgumentOutOfRangeException(nameof(key), key, "未知代理配置项。");
            }

            return SaveValue(PersonalProxyField, value);
        }
    }

    private sealed class PushSection : ZzzConfigSectionViewModel
    {
        private readonly ZzzConfigField _sendImageField = Bool("send_image", true);
        private readonly ZzzConfigField _proxyField = Text("proxy", "NONE");
        private readonly IReadOnlyList<ZzzConfigField> _fields;
        private readonly IReadOnlyDictionary<string, ZzzConfigField> _fieldsByKey;

        public PushSection(
            IZzzAppBackend backend,
            IReadOnlyList<ZzzPushChannelDescriptor> channels,
            Action<string?> errorReporter)
            : base(backend, errorReporter)
        {
            List<ZzzConfigField> fields = [_sendImageField, _proxyField];
            foreach (ZzzPushFieldDescriptor descriptor in channels.SelectMany(channel => channel.Fields))
            {
                if (fields.All(field => !string.Equals(field.Key, descriptor.Key, StringComparison.Ordinal)))
                {
                    fields.Add(Text(descriptor.Key, descriptor.DefaultValue));
                }
            }

            _fields = fields;
            _fieldsByKey = fields.ToDictionary(field => field.Key, StringComparer.Ordinal);
        }

        protected override string ScopeName => "push";
        protected override IReadOnlyList<ZzzConfigField> Fields => _fields;

        public bool SendImage { get => GetValue<bool>(_sendImageField); set => SetValue(_sendImageField, value); }
        public string Proxy { get => GetValue<string>(_proxyField); set => SetValue(_proxyField, value); }

        public object? GetFieldValue(string key, string defaultValue)
        {
            if (!_fieldsByKey.TryGetValue(key, out ZzzConfigField? field))
            {
                return defaultValue;
            }

            return field.ClrType == typeof(bool) ? GetValue<bool>(field) : GetValue<string>(field);
        }

        public bool SaveFieldValue(string key, object? value)
        {
            if (!_fieldsByKey.TryGetValue(key, out ZzzConfigField? field))
            {
                throw new ArgumentOutOfRangeException(nameof(key), key, "未知推送配置项。");
            }

            return SaveValue(field, value);
        }
    }

    private static ZzzConfigField Bool(string key, bool defaultValue) => new(key, typeof(bool), defaultValue, ReadBool);
    private static ZzzConfigField Text(string key, string defaultValue) => new(key, typeof(string), defaultValue, ReadScalar);

    private static object? ReadBool(object? value) => value is JsonElement element
        ? element.ValueKind == JsonValueKind.True
        : value;

    private static object? ReadScalar(object? value) => value is JsonElement element
        ? element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.ToString()
        : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
