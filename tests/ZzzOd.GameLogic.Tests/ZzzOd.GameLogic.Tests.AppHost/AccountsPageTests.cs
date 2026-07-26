using System.Reflection;
using System.Threading.Channels;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Pages.Accounts;
using ZzzOd.Gui.Views.FrontierPages.Accounts;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class AccountsPageTests
{
    private class AccountsBackendProxy : DispatchProxy
    {
        private readonly ZzzBackendEventBus _events = new();
        private List<ZzzInstanceDto> _instances =
        [
            new ZzzInstanceDto(0, "主号", Active: true, "config/00", ActiveInOneDragon: true),
        ];

        public int InstanceScopeReadCount { get; private set; }

        public int UpdateInstanceCallCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            object?[] parameters = args ?? [];
            return targetMethod.Name switch
            {
                nameof(IZzzAppBackend.GetCurrentRun) => ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(ZzzRunState.Idle)),
                nameof(IZzzAppBackend.GetInstances) => ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Ok(_instances),
                nameof(IZzzAppBackend.GetCurrentInstance) => ZzzBackendResult<ZzzInstanceDto>.Ok(_instances.Single(instance => instance.Active)),
                nameof(IZzzAppBackend.GetConfigScope) => GetConfigScope(Required<string>(parameters[0]), parameters[1] is int index ? index : null),
                nameof(IZzzAppBackend.UpdateInstance) => UpdateInstance(Required<ZzzUpdateInstanceRequest>(parameters[0])),
                nameof(IZzzAppBackend.SubscribeEvents) => _events.Subscribe(),
                nameof(IZzzAppBackend.UnsubscribeEvents) => UnsubscribeEvents(Required<ChannelReader<ZzzBackendEvent>>(parameters[0])),
                _ => throw new NotSupportedException(targetMethod.Name),
            };
        }

        private static T Required<T>(object? value)
            where T : class => value as T ?? throw new ArgumentNullException(nameof(value));

        private ZzzBackendResult<ZzzConfigScopeValuesDto> GetConfigScope(string scope, int? instanceIndex)
        {
            if (scope == "instance")
            {
                InstanceScopeReadCount++;
            }

            IReadOnlyDictionary<string, object?> values = scope == "one-dragon"
                ? new Dictionary<string, object?>
                {
                    ["instance_list"] = _instances.Select(instance => new OneDragonInstanceConfigItem
                    {
                        Idx = instance.Index,
                        Name = instance.Name,
                        Active = instance.Active,
                        ActiveInOneDragon = instance.ActiveInOneDragon,
                    }).ToList(),
                }
                : new Dictionary<string, object?>();
            ZzzConfigScopeDescriptorDto descriptor = new(scope, scope, scope == "instance", GroupBound: false, Writable: true, []);
            return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(descriptor, instanceIndex, null, values));
        }

        private ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> UpdateInstance(ZzzUpdateInstanceRequest request)
        {
            UpdateInstanceCallCount++;
            int index = _instances.FindIndex(instance => instance.Index == request.Index);
            if (index < 0)
            {
                return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.NotFound, "实例不存在。");
            }

            ZzzInstanceDto current = _instances[index];
            _instances[index] = current with
            {
                Name = request.Name ?? current.Name,
                ActiveInOneDragon = request.ActiveInOneDragon ?? current.ActiveInOneDragon,
            };
            IReadOnlyList<ZzzInstanceDto> result = _instances;
            _events.Publish("instance.changed", result);
            return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Ok(result);
        }

        private object? UnsubscribeEvents(ChannelReader<ZzzBackendEvent> reader)
        {
            _events.Unsubscribe(reader);
            return null;
        }
    }

    [Fact]
    public void AccountsPageInitialBindingsDoNotSaveAndUserEditsSaveOnce()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, AccountsBackendProxy>();
        AccountsBackendProxy proxy = Assert.IsAssignableFrom<AccountsBackendProxy>(backend);
        GuiParityAndFacadeTests.RunOnUiThread(() =>
        {
            ZzzFrontierAccountsPage page = new(backend);
            try
            {
                page.OnPageShown();

                Assert.Equal(0, proxy.UpdateInstanceCallCount);
                Assert.Equal(1, proxy.InstanceScopeReadCount);

                ZzzAccountRunOption[] runOptions =
                [
                    new ZzzAccountRunOption("一条龙中运行", true),
                    new ZzzAccountRunOption("一条龙中不运行", false),
                ];
                ZzzAccountInstanceRow initialRow = CreateRow(backend, runOptions);
                InvokePageHandler(page, "OnInstanceNameChanged", new TextBox { DataContext = initialRow });
                InvokePageHandler(page, "OnInstanceRunChanged", new FAComboBox { DataContext = initialRow });
                Assert.Equal(0, proxy.UpdateInstanceCallCount);

                initialRow.Name = "主号改名";
                InvokePageHandler(page, "OnInstanceNameChanged", new TextBox { DataContext = initialRow });
                Assert.Equal(1, proxy.UpdateInstanceCallCount);

                ZzzAccountInstanceRow reloadedNameRow = CreateRow(backend, runOptions);
                InvokePageHandler(page, "OnInstanceNameChanged", new TextBox { DataContext = reloadedNameRow });
                Assert.Equal(1, proxy.UpdateInstanceCallCount);

                reloadedNameRow.SelectedRunOption = runOptions[1];
                InvokePageHandler(page, "OnInstanceRunChanged", new FAComboBox { DataContext = reloadedNameRow });
                Assert.Equal(2, proxy.UpdateInstanceCallCount);

                ZzzAccountInstanceRow reloadedRunRow = CreateRow(backend, runOptions);
                InvokePageHandler(page, "OnInstanceRunChanged", new FAComboBox { DataContext = reloadedRunRow });
                Assert.Equal(2, proxy.UpdateInstanceCallCount);
            }
            finally
            {
                page.DisposePage();
            }
        });
    }

    private static ZzzAccountInstanceRow CreateRow(IZzzAppBackend backend, IReadOnlyList<ZzzAccountRunOption> runOptions)
    {
        ZzzInstanceDto instance = backend.GetInstances().Value!.Single();
        return new ZzzAccountInstanceRow(instance, canSwitch: true, instanceCount: 1, runOptions);
    }

    private static void InvokePageHandler(ZzzFrontierAccountsPage page, string methodName, Control sender)
    {
        MethodInfo method = typeof(ZzzFrontierAccountsPage).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(page, [sender, null]);
    }
}
