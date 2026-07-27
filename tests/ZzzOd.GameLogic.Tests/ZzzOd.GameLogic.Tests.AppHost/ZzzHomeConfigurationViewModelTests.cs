using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.PageModels.Home;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzHomeConfigurationViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> ProjectValues { get; } = new(StringComparer.Ordinal)
        {
            ["home_page_link"] = "https://example.test/home",
            ["github_homepage"] = "https://example.test/github",
            ["doc_link"] = "https://example.test/docs",
            ["qq_link"] = "https://example.test/channel",
            ["notice_url"] = "https://example.test/notices.json",
        };

        public Dictionary<string, object?> CustomValues { get; } = new(StringComparer.Ordinal)
        {
            ["custom_theme_color"] = false,
            ["global_theme_color"] = "71,104,179",
        };

        public List<string> GetRequests { get; } = [];

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            switch (targetMethod.Name)
            {
                case nameof(IZzzAppBackend.GetConfigScope):
                {
                    string scope = Assert.IsType<string>(args![0]);
                    GetRequests.Add(scope);
                    return ScopeResult(scope);
                }
                case nameof(IZzzAppBackend.SaveConfigScope) when args is [ZzzSaveConfigScopeRequest request]:
                    SaveRequests.Add(request);
                    Dictionary<string, object?> values = ValuesFor(request.Scope);
                    foreach ((string key, object? value) in request.Values)
                    {
                        values[key] = value;
                    }

                    return ScopeResult(request.Scope);
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }

        private Dictionary<string, object?> ValuesFor(string scope) => scope switch
        {
            "project" => ProjectValues,
            "custom" => CustomValues,
            _ => throw new InvalidOperationException(scope),
        };

        private ZzzBackendResult<ZzzConfigScopeValuesDto> ScopeResult(string scope) =>
            ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
                new ZzzConfigScopeDescriptorDto(scope, scope, false, false, true, []),
                null,
                null,
                new Dictionary<string, object?>(ValuesFor(scope), StringComparer.Ordinal)));
    }

    [Fact]
    public void ProjectScopeLoadsQuickLinksAndNoticeUrlWithOneRead()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzHomeProjectSettingsViewModel viewModel = new(backend);

        viewModel.OnPageShown();

        Assert.Equal("project", Assert.Single(proxy.GetRequests));
        Assert.Equal("https://example.test/notices.json", viewModel.NoticeUrl);
        Assert.Equal(
            [
                "https://example.test/home",
                "https://example.test/github",
                "https://example.test/docs",
                "https://example.test/channel",
            ],
            viewModel.QuickLinks.Select(link => link.Uri));
        Assert.Empty(proxy.SaveRequests);
    }

    [Fact]
    public void ThemeScopeReloadsAndAlwaysPersistsExtractedColor()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzHomeThemeSettingsViewModel viewModel = new(backend);

        viewModel.Reload();
        viewModel.SaveExtractedThemeColor("71,104,179");

        Assert.Equal("custom", Assert.Single(proxy.GetRequests));
        ZzzSaveConfigScopeRequest request = Assert.Single(proxy.SaveRequests);
        Assert.Equal("custom", request.Scope);
        KeyValuePair<string, object?> saved = Assert.Single(request.Values);
        Assert.Equal("global_theme_color", saved.Key);
        Assert.Equal("71,104,179", saved.Value);
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
