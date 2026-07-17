using Avalonia.Controls;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Services.Windows;

public sealed class ZzzWindowBackdropService
{
    private readonly IZzzAppBackend _backend;

    public ZzzWindowBackdropService(IZzzAppBackend backend)
    {
        _backend = backend;
    }

    public WindowTransparencyLevel? ActualLevel { get; private set; }

    public void Apply(Window window)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> custom = _backend.GetConfigScope("custom");
        string? preset = custom.Success && custom.Value is not null && custom.Value.Values.TryGetValue("fluent_visual_preset", out object? raw)
            ? raw?.ToString()
            : null;
        if (!string.Equals(preset, "store-fluent", StringComparison.Ordinal))
        {
            window.TransparencyLevelHint = [WindowTransparencyLevel.None];
            ActualLevel = window.ActualTransparencyLevel;
            return;
        }

        window.TransparencyLevelHint =
        [
            WindowTransparencyLevel.Mica,
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.Blur,
            WindowTransparencyLevel.None,
        ];
        ActualLevel = window.ActualTransparencyLevel;
        window.PropertyChanged += (_, args) =>
        {
            if (args.Property == TopLevel.ActualTransparencyLevelProperty)
            {
                ActualLevel = window.ActualTransparencyLevel;
            }
        };
    }
}
