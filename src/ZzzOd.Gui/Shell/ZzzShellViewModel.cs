using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Shell;

public sealed class ZzzShellViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IZzzAppBackend _backend;
    private readonly IZzzUiDispatcher _dispatcher;
    private readonly ChannelReader<ZzzBackendEvent> _events;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private string _projectName = string.Empty;
    private string _activeInstanceName = string.Empty;
    private string _launcherVersion = string.Empty;
    private string _codeVersion = string.Empty;
    private string _issueUrl = string.Empty;

    public ZzzShellViewModel(IZzzAppBackend backend, IZzzUiDispatcher dispatcher)
    {
        _backend = backend;
        _dispatcher = dispatcher;
        Refresh();
        _events = backend.SubscribeEvents();
        _ = ObserveEventsAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ProjectName => _projectName;

    public string ActiveInstanceName => _activeInstanceName;

    public string WindowTitle => string.Join(' ', new[] { _projectName, _activeInstanceName }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public string LauncherVersionText => string.IsNullOrWhiteSpace(_launcherVersion)
        ? string.Empty
        : $"ⓘ 启动器版本 {_launcherVersion}";

    public string CodeVersionText => string.IsNullOrWhiteSpace(_codeVersion)
        ? string.Empty
        : $"ⓘ 代码版本 {_codeVersion}";

    public bool HasLauncherVersion => !string.IsNullOrWhiteSpace(_launcherVersion);

    public string LauncherVersion => _launcherVersion;

    public bool HasCodeVersion => !string.IsNullOrWhiteSpace(_codeVersion);

    public string CodeVersion => _codeVersion;

    public bool HasIssueUrl => !string.IsNullOrWhiteSpace(_issueUrl);

    public string IssueUrl => _issueUrl;

    public void Refresh()
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> project = _backend.GetConfigScope("project");
        _projectName = LocalizeProjectName(ReadString(project, "project_name"));
        string githubHomepage = ReadString(project, "github_homepage");
        _issueUrl = string.IsNullOrWhiteSpace(githubHomepage)
            ? string.Empty
            : $"{githubHomepage.TrimEnd('/')}/issues";

        ZzzBackendResult<ZzzInstanceDto> instance = _backend.GetCurrentInstance();
        _activeInstanceName = instance.Success ? instance.Value?.Name ?? string.Empty : string.Empty;

        string informationalVersion = typeof(ZzzShellViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? string.Empty;
        _launcherVersion = PackageVersion(informationalVersion);
        _codeVersion = ShortCommit(FirstNonEmpty(
            Environment.GetEnvironmentVariable("GIT_COMMIT"),
            Environment.GetEnvironmentVariable("BUILD_SOURCEVERSION"),
            CommitFromInformationalVersion(informationalVersion)));

        NotifyAll();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _backend.UnsubscribeEvents(_events);
        _cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task ObserveEventsAsync()
    {
        try
        {
            await foreach (ZzzBackendEvent item in _events.ReadAllAsync(_cancellationTokenSource.Token).ConfigureAwait(false))
            {
                if (item.Type is "instance.activeChanged" or "instance.changed")
                {
                    _dispatcher.Post(Refresh);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ChannelClosedException)
        {
        }
    }

    private static string ReadString(ZzzBackendResult<ZzzConfigScopeValuesDto> result, string key)
    {
        if (!result.Success || result.Value?.Values.TryGetValue(key, out object? value) != true)
        {
            return string.Empty;
        }

        return value?.ToString()?.Trim() ?? string.Empty;
    }

    private static string LocalizeProjectName(string projectName) => projectName switch
    {
        "ZenlessZoneZero-OneDragon" => "绝区零 一条龙",
        _ => projectName,
    };

    private static string PackageVersion(string informationalVersion)
    {
        int separator = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        return (separator >= 0 ? informationalVersion[..separator] : informationalVersion).Trim();
    }

    private static string CommitFromInformationalVersion(string informationalVersion)
    {
        int separator = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        return separator >= 0 && separator + 1 < informationalVersion.Length
            ? informationalVersion[(separator + 1)..].Trim()
            : string.Empty;
    }

    private static string ShortCommit(string commit)
    {
        string normalized = commit.Trim();
        return normalized.Length > 8 ? normalized[..8] : normalized;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(ActiveInstanceName));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(LauncherVersionText));
        OnPropertyChanged(nameof(CodeVersionText));
        OnPropertyChanged(nameof(HasLauncherVersion));
        OnPropertyChanged(nameof(LauncherVersion));
        OnPropertyChanged(nameof(HasCodeVersion));
        OnPropertyChanged(nameof(CodeVersion));
        OnPropertyChanged(nameof(HasIssueUrl));
        OnPropertyChanged(nameof(IssueUrl));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
