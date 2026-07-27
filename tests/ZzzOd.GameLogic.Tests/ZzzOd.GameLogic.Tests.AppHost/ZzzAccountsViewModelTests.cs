using System.Reflection;
using System.Threading.Channels;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.PageModels.Accounts;
using ZzzOd.Gui.Views.FrontierPages.Accounts;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzAccountsViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        private readonly Channel<ZzzBackendEvent> _events = Channel.CreateUnbounded<ZzzBackendEvent>();

        public Dictionary<string, object?> InstanceValues { get; } = new(StringComparer.Ordinal)
        {
            ["game_path"] = "D:\\Games\\ZenlessZoneZero.exe",
            ["use_custom_win_title"] = true,
            ["custom_win_title"] = "ZZZ",
            ["game_region"] = "cn",
            ["account"] = "user@example.com",
            ["password"] = "secret",
            ["bilibili_account_name"] = string.Empty,
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        public int InstanceScopeReadCount { get; private set; }

        public bool FailInstanceList { get; set; }

        public string? SaveError { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            object?[] parameters = args ?? [];
            return targetMethod.Name switch
            {
                nameof(IZzzAppBackend.GetCurrentRun) => ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(ZzzRunState.Idle)),
                nameof(IZzzAppBackend.GetInstances) => GetInstances(),
                nameof(IZzzAppBackend.GetCurrentInstance) => ZzzBackendResult<ZzzInstanceDto>.Ok(Instance()),
                nameof(IZzzAppBackend.GetConfigScope) => GetConfigScope((string)parameters[0], parameters[1] as int?),
                nameof(IZzzAppBackend.SaveConfigScope) => Save((ZzzSaveConfigScopeRequest)parameters[0]),
                nameof(IZzzAppBackend.SubscribeEvents) => _events.Reader,
                nameof(IZzzAppBackend.UnsubscribeEvents) => null,
                _ => throw new NotSupportedException(targetMethod.Name),
            };
        }

        private ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> GetInstances() =>
            FailInstanceList
                ? ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.NotReady, "账户列表不可用")
                : ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Ok([Instance()]);

        private ZzzBackendResult<ZzzConfigScopeValuesDto> GetConfigScope(string scope, int? instanceIndex)
        {
            if (scope == "instance")
            {
                InstanceScopeReadCount++;
                return Snapshot(scope, instanceIndex, InstanceValues);
            }

            return Snapshot(
                scope,
                instanceIndex,
                new Dictionary<string, object?>
                {
                    ["instance_list"] = new List<ZzzOd.GameLogic.Config.OneDragonInstanceConfigItem>
                    {
                        new() { Idx = 0, Name = "主号", Active = true, ActiveInOneDragon = true },
                    },
                });
        }

        private ZzzBackendResult<ZzzConfigScopeValuesDto> Save(ZzzSaveConfigScopeRequest request)
        {
            SaveRequests.Add(request);
            if (SaveError is not null)
            {
                return ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(ZzzBackendErrorCode.Validation, SaveError);
            }

            foreach ((string key, object? value) in request.Values)
            {
                InstanceValues[key] = value;
            }

            return Snapshot("instance", request.InstanceIndex, InstanceValues);
        }

        private static ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot(
            string scope,
            int? instanceIndex,
            IReadOnlyDictionary<string, object?> values)
        {
            ZzzConfigScopeDescriptorDto descriptor = new(scope, scope, scope == "instance", false, true, []);
            return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(
                new ZzzConfigScopeValuesDto(
                    descriptor,
                    instanceIndex,
                    null,
                    new Dictionary<string, object?>(values, StringComparer.Ordinal)));
        }

        private static ZzzInstanceDto Instance() =>
            new(0, "主号", Active: true, "config/00", ActiveInOneDragon: true);
    }

    [Fact]
    public void CurrentAccountSettingsLoadOnceAndSaveThroughBindingLayer()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzCurrentAccountSettingsPage viewModel = new(backend);

        viewModel.OnPageShown();
        viewModel.CustomWindowTitle = "ZZZ New";
        viewModel.SelectedGameRegion = viewModel.RegionOptions.Single(option => option.Value == "cn_b");

        Assert.Equal(1, proxy.InstanceScopeReadCount);
        Assert.Equal("D:\\Games\\ZenlessZoneZero.exe", viewModel.GamePath);
        Assert.Equal("ZZZ New", viewModel.CustomWindowTitle);
        Assert.False(viewModel.AccountPasswordVisible);
        Assert.True(viewModel.BilibiliVisible);
        Assert.Equal(["custom_win_title", "game_region"], proxy.SaveRequests.Select(request => Assert.Single(request.Values).Key));
        Assert.All(proxy.SaveRequests, request => Assert.Equal(0, request.InstanceIndex));
    }

    [Fact]
    public void SaveFailureKeepsAccountInputAndReportsBackendError()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzCurrentAccountSettingsPage viewModel = new(backend);
        viewModel.OnPageShown();
        proxy.SaveError = "账号保存失败";

        viewModel.Account = "new@example.com";

        Assert.Equal("new@example.com", viewModel.Account);
        Assert.Equal("账号保存失败", viewModel.LastError);
    }

    [Fact]
    public void InstanceLoadFailureUsesExistingInfoBarWithoutEscapingPageLifecycle()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        proxy.FailInstanceList = true;

        GuiParityAndFacadeTests.RunOnUiThread(() =>
        {
            ZzzFrontierAccountsPage page = new(backend);
            try
            {
                Exception? exception = Record.Exception(page.OnPageShown);
                FAInfoBar actionBar = page.FindControl<FAInfoBar>("ActionBar")!;

                Assert.Null(exception);
                Assert.True(actionBar.IsOpen);
                Assert.Equal("账户列表不可用", actionBar.Message);
                Assert.Equal(FAInfoBarSeverity.Error, actionBar.Severity);
            }
            finally
            {
                page.DisposePage();
            }
        });
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
