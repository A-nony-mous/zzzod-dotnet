using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.RandomPlay;
using ZzzOd.GameLogic.GameData;
using ZzzOd.Gui.Pages.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class RandomPlaySettingsFlyoutTests
{
	public class RecordingBackendProxy : DispatchProxy
	{
		public Dictionary<string, object?> Values { get; } = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["transport_point"] = "柜台",
			["agent_name_1"] = "安比",
			["agent_name_2"] = "随机"
		};

		public List<ZzzSaveConfigScopeRequest> Requests { get; } = new List<ZzzSaveConfigScopeRequest>();

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			ArgumentNullException.ThrowIfNull(targetMethod, "targetMethod");
			object value;
			if (targetMethod.Name == "GetConfigScope" && args != null && args.Length == 3 && args[0] is string scope)
			{
				value = args[1];
				if (value is int value2 && args[2] is string groupId)
				{
					return Snapshot(scope, value2, groupId);
				}
			}
			if (targetMethod.Name == "SaveConfigScope" && args != null && args.Length == 1 && args[0] is ZzzSaveConfigScopeRequest zzzSaveConfigScopeRequest)
			{
				Requests.Add(zzzSaveConfigScopeRequest);
				foreach (KeyValuePair<string, object> value4 in zzzSaveConfigScopeRequest.Values)
				{
					value4.Deconstruct(out var key, out value);
					string key2 = key;
					object value3 = value;
					Values[key2] = value3;
				}
				return Snapshot(zzzSaveConfigScopeRequest.Scope, zzzSaveConfigScopeRequest.InstanceIndex, zzzSaveConfigScopeRequest.GroupId);
			}
			throw new NotSupportedException(targetMethod.Name);
		}

		private ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot(string scope, int? instanceIndex, string groupId)
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(new ZzzConfigScopeDescriptorDto(scope, "录像店营业", InstanceBound: true, GroupBound: true, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>()), instanceIndex, groupId, new Dictionary<string, object>(Values, StringComparer.Ordinal)));
		}
	}

	[Fact]
	public void ViewModelUsesRealTransportAndAgentCatalogsInPythonOrder()
	{
		IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
		ZzzRandomPlaySettingsFlyoutViewModel zzzRandomPlaySettingsFlyoutViewModel = new ZzzRandomPlaySettingsFlyoutViewModel(backend, 5, "daily");
		Assert.Equal(RandomPlayTransportPoint.All.Select((RandomPlayTransportPoint point) => point.Value), zzzRandomPlaySettingsFlyoutViewModel.TransportPointOptions.Select((ZzzRandomPlaySettingOption option) => option.Value));
		Assert.Equal("录像店 - 柜台", zzzRandomPlaySettingsFlyoutViewModel.TransportPointOptions[0].Label);
		Assert.Equal("澄辉坪 - 录像店营业点", zzzRandomPlaySettingsFlyoutViewModel.TransportPointOptions[1].Label);
		Assert.Equal("随机", zzzRandomPlaySettingsFlyoutViewModel.AgentOptions[0].Value);
		Assert.Equal(AgentEnum.Values.Select((AgentEnum agent) => agent.Value.AgentName), from option in zzzRandomPlaySettingsFlyoutViewModel.AgentOptions.Skip(1)
			select option.Value);
	}
}
