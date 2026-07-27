using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.BattleAssistant;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.PageModels.GameAssistant;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzBattleAssistantSettingsViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, Dictionary<string, object?>> Scopes { get; } = new(StringComparer.Ordinal)
        {
            ["battle-assistant"] = new(StringComparer.Ordinal)
            {
                ["auto_battle_config"] = "配置A",
                ["dodge_assistant_config"] = "闪避A",
                ["auto_ultimate_enabled"] = false,
                ["use_merged_file"] = true,
                ["screenshot_interval"] = 0.02d,
                ["control_method"] = BattleAssistantConfig.ControlMethodKeyboard,
            },
            ["model"] = new(StringComparer.Ordinal)
            {
                ["flash_classifier_gpu"] = true,
            },
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        public string? SaveError { get; set; }

        public ZzzBattleAssistantConfigCatalogDto Catalog { get; set; } =
            new(["配置A", "配置B"], ["闪避A", "闪避B"]);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            switch (targetMethod.Name)
            {
                case nameof(IZzzAppBackend.GetConfigScope):
                {
                    string scope = Assert.IsType<string>(args![0]);
                    Dictionary<string, object?> values = Scopes[scope];
                    ZzzConfigScopeDescriptorDto descriptor = new(scope, scope, false, false, true, []);
                    return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
                        descriptor,
                        null,
                        null,
                        new Dictionary<string, object?>(values, StringComparer.Ordinal)));
                }
                case nameof(IZzzAppBackend.SaveConfigScope) when args is [ZzzSaveConfigScopeRequest request]:
                    SaveRequests.Add(request);
                    if (SaveError is not null)
                    {
                        return ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(ZzzBackendErrorCode.Validation, SaveError);
                    }

                    foreach ((string key, object? value) in request.Values)
                    {
                        Scopes[request.Scope][key] = value;
                    }

                    return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
                        new ZzzConfigScopeDescriptorDto(request.Scope, request.Scope, false, false, true, []),
                        null,
                        null,
                        new Dictionary<string, object?>(Scopes[request.Scope], StringComparer.Ordinal)));
                case nameof(IZzzAppBackend.GetBattleAssistantConfigCatalog):
                    return ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto>.Ok(Catalog);
                case nameof(IZzzAppBackend.DeleteBattleAssistantConfig):
                    return ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto>.Ok(Catalog);
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }
    }

    [Fact]
    public void ReloadLoadsBothScopesAndCatalogWithoutWriting()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzBattleAssistantSettingsViewModel viewModel = new(backend);

        viewModel.OnPageShown();

        Assert.Empty(proxy.SaveRequests);
        Assert.Equal(["配置A", "配置B"], viewModel.AutoBattleOptions);
        Assert.Equal("配置A", viewModel.SelectedAutoBattleConfig);
        Assert.False(viewModel.AutoUltimateEnabled);
        Assert.True(viewModel.FlashClassifierGpu);
        Assert.Equal("键鼠", viewModel.SelectedControlMethod?.Label);
    }

    [Fact]
    public void BoundSettingsSaveSingleFieldsThroughTheirOwnScopes()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzBattleAssistantSettingsViewModel viewModel = new(backend);
        viewModel.OnPageShown();

        viewModel.AutoUltimateEnabled = true;
        viewModel.FlashClassifierGpu = false;
        viewModel.ScreenshotInterval = 0.05d;
        viewModel.SelectedControlMethod = viewModel.ControlMethodOptions.Single(option => option.Value == "ds4");

        Assert.Equal(4, proxy.SaveRequests.Count);
        Assert.Equal("battle-assistant", proxy.SaveRequests[0].Scope);
        Assert.Equal("model", proxy.SaveRequests[1].Scope);
        Assert.Equal(0.05d, proxy.SaveRequests[2].Values["screenshot_interval"]);
        Assert.Equal("ds4", proxy.SaveRequests[3].Values["control_method"]);
    }

    [Fact]
    public void SaveFailureKeepsInputAndReportsError()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzBattleAssistantSettingsViewModel viewModel = new(backend);
        viewModel.OnPageShown();
        proxy.SaveError = "战斗助手配置保存失败";

        viewModel.AutoUltimateEnabled = true;

        Assert.True(viewModel.AutoUltimateEnabled);
        Assert.Contains("战斗助手配置保存失败", viewModel.LastError ?? string.Empty);
        Assert.True(viewModel.HasError);
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
