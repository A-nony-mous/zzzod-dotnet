using System.Reflection;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Notifications;
using ZzzOd.Gui.Views.FrontierPages.Settings;
using Xunit;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class PushSettingsViewModelTests
{
    [Fact]
    public void LoadsThreeScopesAndSavesEachThroughItsSection()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, PushSettingsAxamlPageTests.RecordingBackendProxy>();
        PushSettingsAxamlPageTests.RecordingBackendProxy proxy =
            (PushSettingsAxamlPageTests.RecordingBackendProxy)backend;
        ZzzPushNotificationService service = new(new ZzzRunRoot(Path.GetTempPath()));
        ZzzPushSettingsViewModel viewModel = new(backend, service);

        viewModel.OnPageShown();

        Assert.Equal("一条龙运行通知", viewModel.Title);
        Assert.True(viewModel.SendImage);
        Assert.Equal("NONE", viewModel.SelectedProxy?.Value);
        Assert.Equal("https://example.invalid/hook", viewModel.GetPushValue("webhook_url", string.Empty));

        viewModel.Title = "运行完成";
        viewModel.SendImage = false;
        viewModel.SelectedProxy = viewModel.ProxyOptions.Single(option => option.Value == "PERSONAL");
        viewModel.PersonalProxy = "http://127.0.0.1:8080";

        Assert.Equal("运行完成", proxy.Scopes["notify"]["title"]);
        Assert.Equal(false, proxy.Scopes["push"]["send_image"]);
        Assert.Equal("PERSONAL", proxy.Scopes["push"]["proxy"]);
        Assert.Equal("http://127.0.0.1:8080", proxy.Scopes["env"]["personal_proxy"]);
        Assert.Null(viewModel.LastError);
    }

    [Fact]
    public void SaveFailureKeepsInputAndReportsBackendError()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, FailingBackendProxy>();
        FailingBackendProxy proxy = (FailingBackendProxy)backend;
        ZzzPushNotificationService service = new(new ZzzRunRoot(Path.GetTempPath()));
        string? error = null;
        ZzzPushSettingsViewModel viewModel = new(backend, service, value => error = value);
        viewModel.OnPageShown();
        proxy.FailSave = true;

        viewModel.Title = "用户输入";

        Assert.Equal("用户输入", viewModel.Title);
        Assert.Equal("通知保存失败。", error);
        Assert.Equal("通知保存失败。", viewModel.LastError);
    }

    public class FailingBackendProxy : DispatchProxy
    {
        private static readonly IReadOnlyDictionary<string, ZzzConfigScopeDescriptorDto> Descriptors =
            new Dictionary<string, ZzzConfigScopeDescriptorDto>(StringComparer.Ordinal)
            {
                ["notify"] = Descriptor("notify"),
                ["push"] = Descriptor("push"),
                ["env"] = Descriptor("env"),
            };

        public bool FailSave { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            args ??= [];
            if (targetMethod.Name == "GetConfigScope")
            {
                string scope = (string)args[0]!;
                return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
                    Descriptors[scope],
                    null,
                    null,
                    Values(scope)));
            }

            if (targetMethod.Name == "SaveConfigScope")
            {
                ZzzSaveConfigScopeRequest request = (ZzzSaveConfigScopeRequest)args[0]!;
                if (FailSave)
                {
                    return ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(
                        ZzzBackendErrorCode.Validation,
                        "通知保存失败。");
                }

                return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
                    Descriptors[request.Scope],
                    null,
                    null,
                    request.Values));
            }

            throw new NotSupportedException(targetMethod.Name);
        }

        private static ZzzConfigScopeDescriptorDto Descriptor(string scope) => new(
            scope,
            scope,
            InstanceBound: false,
            GroupBound: false,
            Writable: true,
            []);

        private static IReadOnlyDictionary<string, object?> Values(string scope) => scope switch
        {
            "notify" => new Dictionary<string, object?> { ["title"] = "一条龙运行通知" },
            "env" => new Dictionary<string, object?> { ["personal_proxy"] = string.Empty },
            _ => new Dictionary<string, object?>
            {
                ["send_image"] = true,
                ["proxy"] = "NONE",
            },
        };
    }
}
