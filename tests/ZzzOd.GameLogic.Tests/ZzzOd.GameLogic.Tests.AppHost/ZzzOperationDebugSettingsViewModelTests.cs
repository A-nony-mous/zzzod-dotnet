using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.BattleAssistant;
using ZzzOd.GameLogic.Application.Devtools.OperationDebug;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.PageModels.Devtools;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzOperationDebugSettingsViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public string RunRoot { get; set; } = string.Empty;

        public Dictionary<string, Dictionary<string, object?>> Scopes { get; } = new(StringComparer.Ordinal)
        {
            ["operation-debug"] = new(StringComparer.Ordinal)
            {
                ["operation_template"] = "sub/beta",
                ["repeat_enabled"] = true,
            },
            ["battle-assistant"] = new(StringComparer.Ordinal)
            {
                ["control_method"] = BattleAssistantConfig.ControlMethodDs4,
            },
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            switch (targetMethod.Name)
            {
                case nameof(IZzzAppBackend.GetCurrentInstance):
                    return ZzzBackendResult<ZzzInstanceDto>.Ok(new ZzzInstanceDto(7, "实例 07", true, "config/07"));
                case nameof(IZzzAppBackend.GetHealth):
                    return ZzzBackendResult<ZzzHealthDto>.Ok(new ZzzHealthDto(ZzzHostMode.Gui, "test", RunRoot, false, true, 7));
                case nameof(IZzzAppBackend.GetConfigScope) when args is [string scope, ..]:
                    return ScopeResult(scope);
                case nameof(IZzzAppBackend.SaveConfigScope) when args is [ZzzSaveConfigScopeRequest request]:
                    SaveRequests.Add(request);
                    foreach ((string key, object? value) in request.Values)
                    {
                        Scopes[request.Scope][key] = value;
                    }

                    return ScopeResult(request.Scope);
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }

        private ZzzBackendResult<ZzzConfigScopeValuesDto> ScopeResult(string scope) =>
            ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
                new ZzzConfigScopeDescriptorDto(scope, scope, true, scope == "operation-debug", true, []),
                7,
                scope == "operation-debug" ? OperationDebugConstants.DefaultGroupId : null,
                new Dictionary<string, object?>(Scopes[scope], StringComparer.Ordinal)));
    }

    [Fact]
    public void ReloadLoadsBothScopesAndTemplateCatalogWithoutWriting()
    {
        string runRoot = Path.Combine(Path.GetTempPath(), "zzz-operation-debug-vm", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(runRoot, "config", "auto_battle_operation", "sub"));
        File.WriteAllText(Path.Combine(runRoot, "config", "auto_battle_operation", "sub", "beta.sample.yml"), "operations: []");
        try
        {
            (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend(runRoot);
            ZzzOperationDebugSettingsViewModel viewModel = new(backend);

            viewModel.OnPageShown();

            Assert.Equal(7, viewModel.ActiveInstanceIndex);
            Assert.Equal(["sub/beta"], viewModel.OperationTemplates);
            Assert.Equal("sub/beta", viewModel.OperationTemplate);
            Assert.True(viewModel.RepeatEnabled);
            Assert.Equal("ds4", viewModel.SelectedControlMethod?.Value);
            Assert.Empty(proxy.SaveRequests);
        }
        finally
        {
            if (Directory.Exists(runRoot))
            {
                Directory.Delete(runRoot, true);
            }
        }
    }

    [Fact]
    public void BoundPropertiesSaveExpectedScopesAndGroups()
    {
        string runRoot = Path.Combine(Path.GetTempPath(), "zzz-operation-debug-vm", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);
        try
        {
            (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend(runRoot);
            ZzzOperationDebugSettingsViewModel viewModel = new(backend);
            viewModel.OnPageShown();

            viewModel.OperationTemplate = "alpha";
            viewModel.RepeatEnabled = false;
            viewModel.SelectedControlMethod = viewModel.ControlMethodOptions.Single(option => option.Value == "keyboard");

            Assert.Equal(3, proxy.SaveRequests.Count);
            Assert.Equal("operation-debug", proxy.SaveRequests[0].Scope);
            Assert.Equal(OperationDebugConstants.DefaultGroupId, proxy.SaveRequests[0].GroupId);
            Assert.Equal("battle-assistant", proxy.SaveRequests[2].Scope);
            Assert.Null(proxy.SaveRequests[2].GroupId);
        }
        finally
        {
            if (Directory.Exists(runRoot))
            {
                Directory.Delete(runRoot, true);
            }
        }
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend(string runRoot)
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        RecordingBackendProxy proxy = (RecordingBackendProxy)backend;
        proxy.RunRoot = runRoot;
        return (backend, proxy);
    }
}
