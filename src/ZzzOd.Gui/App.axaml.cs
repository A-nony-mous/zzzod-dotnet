using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Views;
using System.Text.Json;

namespace ZzzOd.Gui;

public sealed partial class App : Application
{
    private TrayIcon? _trayIcon;
    private ZzzGuiSingleInstanceSignal? _singleInstanceSignal;
    private bool _exitRequested;

    public static IHost? Host { get; set; }

    public static string? RunRoot { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ApplyEvidenceTheme();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (Host is null)
        {
			throw new InvalidOperationException("共享宿主未初始化。");
        }

        Host.StartAsync().GetAwaiter().GetResult();
        IZzzAppBackend backend = Host.Services.GetRequiredService<IZzzAppBackend>();
        ApplyConfiguredAccentColor(backend);
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            MainWindow mainWindow = ActivatorUtilities.CreateInstance<MainWindow>(Host.Services);
            desktop.MainWindow = mainWindow;
            mainWindow.Opened += (_, _) => ZzzGuiControlTreeEvidence.TryWrite(mainWindow);
            InstallTray(desktop, mainWindow);
            InstallCloseToTray(mainWindow);
            if (!string.IsNullOrWhiteSpace(RunRoot))
            {
                _singleInstanceSignal = ZzzGuiSingleInstanceSignal.Start(RunRoot, () =>
                    Dispatcher.UIThread.Post(() => ShowMainWindow(mainWindow)));
            }

            desktop.Exit += (_, _) =>
            {
                _singleInstanceSignal?.Dispose();
                _trayIcon?.Dispose();
                Host.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InstallTray(IClassicDesktopStyleApplicationLifetime desktop, Window mainWindow)
    {
        NativeMenuItem showItem = new("显示窗口");
        NativeMenuItem exitItem = new("退出");
        showItem.Click += (_, _) => ShowMainWindow(mainWindow);
        exitItem.Click += (_, _) =>
        {
            _exitRequested = true;
            desktop.Shutdown(0);
        };

        NativeMenu menu = new();
        menu.Items.Add(showItem);
        menu.Items.Add(exitItem);
        _trayIcon = new TrayIcon
        {
            ToolTipText = "ZZZ OneDragon",
            IsVisible = true,
            Menu = menu,
        };
        _trayIcon.Clicked += (_, _) => ShowMainWindow(mainWindow);
    }

    private void InstallCloseToTray(Window mainWindow)
    {
        mainWindow.Closing += (_, args) =>
        {
            if (_exitRequested)
            {
                return;
            }

            args.Cancel = true;
            mainWindow.Hide();
        };
    }

    private static void ShowMainWindow(Window mainWindow)
    {
        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Show();
        mainWindow.Activate();
    }

    private void ApplyEvidenceTheme()
    {
        string? theme = Environment.GetEnvironmentVariable("ZZZOD_GUI_THEME");
        ThemeVariant? themeVariant = ResolveEvidenceThemeVariant(theme);
        if (themeVariant is not null)
        {
            RequestedThemeVariant = themeVariant;
        }
    }

    internal static ThemeVariant? ResolveEvidenceThemeVariant(string? theme) =>
        theme?.Trim().ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            "highcontrast" or "high-contrast" => FluentAvaloniaTheme.HighContrastTheme,
            _ => null,
        };

    internal static void ApplyAccentColor(Color color)
    {
        FluentAvaloniaTheme? theme = Current?.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();
        if (theme is not null)
        {
            theme.CustomAccentColor = color;
        }
    }

    internal static void ApplyVisualPreset(string value)
    {
        if (Current is not null)
        {
            Current.Resources["ZzzVisualPreset"] = value;
            if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is Window window)
            {
                if (value == "store-fluent")
                {
                    window.Classes.Add("fluent-preset");
                }
                else
                {
                    window.Classes.Remove("fluent-preset");
                }
            }
        }
    }

    private static void ApplyConfiguredVisualPreset(IZzzAppBackend backend)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> custom = backend.GetConfigScope("custom");
        string value = custom.Success && custom.Value is not null && custom.Value.Values.TryGetValue("fluent_visual_preset", out object? raw)
            ? raw?.ToString() ?? "baseline-parity"
            : "baseline-parity";
        ApplyVisualPreset(value);
    }

    internal static void ApplyConfiguredAccentColor(IZzzAppBackend backend)
    {
        if (TryReadConfiguredAccentColor(backend, out Color color))
        {
            ApplyAccentColor(color);
        }
    }

    internal static bool TryReadConfiguredAccentColor(IZzzAppBackend backend, out Color color)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ZzzBackendResult<ZzzConfigScopeValuesDto> custom = backend.GetConfigScope("custom");
        if (custom.Success
            && custom.Value is not null
            && custom.Value.Values.TryGetValue("global_theme_color", out object? rawColor)
            && TryParseRgbColor(rawColor?.ToString(), out color))
        {
            return true;
        }

        color = default;
        return false;
    }

    private static bool TryParseRgbColor(string? value, out Color color)
    {
        string[] parts = value?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (parts.Length == 3
            && byte.TryParse(parts[0], out byte red)
            && byte.TryParse(parts[1], out byte green)
            && byte.TryParse(parts[2], out byte blue))
        {
            color = Color.FromRgb(red, green, blue);
            return true;
        }

        color = default;
        return false;
    }
}

internal static class ZzzGuiControlTreeEvidence
{
    private const string PathVariable = "ZZZOD_GUI_EVIDENCE_CONTROL_TREE_PATH";

    internal static void TryWrite(Visual root)
    {
        string? path = Environment.GetEnvironmentVariable(PathVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (FileStream stream = File.Create(temporaryPath))
            {
                JsonSerializer.Serialize(stream, new
                {
                    schema = "zzzod-avalonia-visual-tree.v1",
                    capturedAt = DateTimeOffset.UtcNow,
                    actualTransparencyLevel = (root as TopLevel)?.ActualTransparencyLevel.ToString(),
                    root = CreateNode(root),
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            File.Move(temporaryPath, fullPath, true);
        }
        catch
        {
        }
    }

    private static object CreateNode(Visual visual) => new
    {
        type = visual.GetType().FullName,
        bounds = new { visual.Bounds.X, visual.Bounds.Y, visual.Bounds.Width, visual.Bounds.Height },
        visible = visual.IsVisible,
        children = visual.GetVisualChildren().Select(CreateNode).ToArray(),
    };
}

