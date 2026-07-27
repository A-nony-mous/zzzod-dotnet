using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.CommissionAssistant;
using ZzzOd.Gui.PageModels.GameAssistant;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzCommissionAssistantSettingsViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal)
        {
            ["pause_in_background"] = true,
            ["dialog_click_interval"] = 0.5d,
            ["story_mode"] = CommissionAssistantStoryMode.Click.Value,
            ["dialog_option"] = CommissionAssistantDialogOption.Last.Value,
            ["dodge_config"] = "闪避A",
            ["dodge_switch"] = "5",
            ["auto_battle"] = "配置A",
            ["auto_battle_switch"] = "6",
            ["sleep_after_empty_screen"] = 0.5d,
        };

        public List<(string Scope, int? InstanceIndex, string? GroupId)> GetRequests { get; } = [];

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
                    int? instanceIndex = args[1] as int?;
                    string? groupId = args[2] as string;
                    GetRequests.Add((scope, instanceIndex, groupId));
                    return ScopeResult(scope);
                }
                case nameof(IZzzAppBackend.SaveConfigScope) when args is [ZzzSaveConfigScopeRequest request]:
                    SaveRequests.Add(request);
                    if (SaveError is not null)
                    {
                        return ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(
                            ZzzBackendErrorCode.Validation,
                            SaveError);
                    }

                    foreach ((string key, object? value) in request.Values)
                    {
                        Values[key] = value;
                    }

                    return ScopeResult(request.Scope);
                case nameof(IZzzAppBackend.GetBattleAssistantConfigCatalog):
                    return ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto>.Ok(Catalog);
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }

        private ZzzBackendResult<ZzzConfigScopeValuesDto> ScopeResult(string scope) =>
            ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
                new ZzzConfigScopeDescriptorDto(scope, scope, false, false, true, []),
                null,
                CommissionAssistantConstants.DefaultGroupId,
                new Dictionary<string, object?>(Values, StringComparer.Ordinal)));
    }

    [Fact]
    public void ReloadLoadsScopeAndCatalogOnceWithoutWriting()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzCommissionAssistantSettingsViewModel viewModel = new(backend);

        viewModel.OnPageShown();

        Assert.Single(proxy.GetRequests);
        Assert.Equal(("commission-assistant", null, CommissionAssistantConstants.DefaultGroupId), proxy.GetRequests[0]);
        Assert.Empty(proxy.SaveRequests);
        Assert.True(viewModel.ConfigValuesAvailable);
        Assert.Equal("闪避A", viewModel.SelectedDodgeConfig);
        Assert.Equal("配置A", viewModel.SelectedAutoBattleConfig);
        Assert.Equal("5", viewModel.DodgeSwitch);
        Assert.Equal("6", viewModel.AutoBattleSwitch);
    }

    [Fact]
    public void NineBoundPropertiesSaveOneFieldWithOneDragonGroup()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzCommissionAssistantSettingsViewModel viewModel = new(backend);
        viewModel.OnPageShown();

        viewModel.PauseInBackground = false;
        viewModel.DialogClickInterval = 0.75d;
        viewModel.StoryMode = CommissionAssistantStoryMode.Skip.Value;
        viewModel.DialogOption = CommissionAssistantDialogOption.First.Value;
        viewModel.SelectedDodgeConfig = "闪避B";
        viewModel.DodgeSwitch = "7";
        viewModel.SelectedAutoBattleConfig = "配置B";
        viewModel.AutoBattleSwitch = "8";
        viewModel.SleepAfterEmptyScreen = 1.5d;

        Assert.Equal(9, proxy.SaveRequests.Count);
        Assert.All(proxy.SaveRequests, request =>
        {
            Assert.Equal("commission-assistant", request.Scope);
            Assert.Equal(CommissionAssistantConstants.DefaultGroupId, request.GroupId);
            Assert.Single(request.Values);
        });
        Assert.Equal(
            [
                "pause_in_background",
                "dialog_click_interval",
                "story_mode",
                "dialog_option",
                "dodge_config",
                "dodge_switch",
                "auto_battle",
                "auto_battle_switch",
                "sleep_after_empty_screen",
            ],
            proxy.SaveRequests.Select(request => request.Values.Keys.Single()));
    }

    [Fact]
    public void SaveFailureKeepsHotkeyInputAndReportsBackendError()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        string? reportedError = null;
        ZzzCommissionAssistantSettingsViewModel viewModel = new(backend, error => reportedError = error);
        viewModel.OnPageShown();
        proxy.SaveError = "委托助手热键保存失败";

        viewModel.DodgeSwitch = "f8";

        Assert.Equal("f8", viewModel.DodgeSwitch);
        Assert.Equal("委托助手热键保存失败", viewModel.LastError);
        Assert.Equal("委托助手热键保存失败", reportedError);
        Assert.Equal("dodge_switch", Assert.Single(proxy.SaveRequests).Values.Keys.Single());
    }

    [Fact]
    public void CatalogDoesNotInjectConfiguredValuesMissingFromDirectories()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        proxy.Values["dodge_config"] = "已删除闪避";
        proxy.Values["auto_battle"] = "已删除配置";
        ZzzCommissionAssistantSettingsViewModel viewModel = new(backend);

        viewModel.OnPageShown();

        Assert.Equal(["闪避A", "闪避B"], viewModel.DodgeOptions);
        Assert.Equal(["配置A", "配置B"], viewModel.AutoBattleOptions);
        Assert.Null(viewModel.SelectedDodgeConfig);
        Assert.Null(viewModel.SelectedAutoBattleConfig);
        Assert.Empty(proxy.SaveRequests);
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
