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
	public void FlyoutUsesAxamlFluentControlsAndPythonTexts()
	{
		string path = FindDirectory();
		string text = File.ReadAllText(Path.Combine(path, "ZzzRandomPlaySettingsFlyoutContent.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzRandomPlaySettingsFlyoutContent.axaml.cs"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "ZzzRandomPlaySettingsFlyoutViewModel.cs"));
		Assert.True(text.IndexOf("传送地点", StringComparison.Ordinal) < text.IndexOf("影像店代理人-1", StringComparison.Ordinal));
		Assert.True(text.IndexOf("影像店代理人-1", StringComparison.Ordinal) < text.IndexOf("影像店代理人-2", StringComparison.Ordinal));
		Assert.Equal(3, Count(text, "fa:SettingsExpanderItem Content="));
		Assert.Equal(3, Count(text, "fa:FAComboBox"));
		Assert.Equal(2, Count(text, "IsEditable=\"True\""));
		Assert.Contains("RandomPlayTransportPoint.All", actualString2, StringComparison.Ordinal);
		Assert.Contains("AgentEnum.Values.Select", actualString2, StringComparison.Ordinal);
		Assert.Contains("RandomPlayConstants.RandomAgentName", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("PageModel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("Python", text, StringComparison.Ordinal);
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

	[Fact]
	public void FlyoutReadsAndWritesRequestedInstanceAndGroup()
	{
		IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
		RecordingBackendProxy recordingBackendProxy = (RecordingBackendProxy)backend;
		GuiParityAndFacadeTests.RunOnUiThread(delegate
		{
			ZzzRandomPlaySettingsFlyoutContent zzzRandomPlaySettingsFlyoutContent = new ZzzRandomPlaySettingsFlyoutContent(backend, 3, "daily");
			Assert.Equal("柜台", zzzRandomPlaySettingsFlyoutContent.ViewModel.TransportPoint);
			Assert.Equal("安比", zzzRandomPlaySettingsFlyoutContent.ViewModel.AgentName1);
			Assert.Equal("随机", zzzRandomPlaySettingsFlyoutContent.ViewModel.AgentName2);
			zzzRandomPlaySettingsFlyoutContent.SaveForTest("transport_point", "录像店营业点");
			zzzRandomPlaySettingsFlyoutContent.SaveForTest("agent_name_1", "妮可");
		});
		Assert.All(recordingBackendProxy.Requests, delegate(ZzzSaveConfigScopeRequest request)
		{
			Assert.Equal("random-play", request.Scope);
			Assert.Equal(3, request.InstanceIndex);
			Assert.Equal("daily", request.GroupId);
		});
		Assert.Equal("录像店营业点", recordingBackendProxy.Values["transport_point"]);
		Assert.Equal("妮可", recordingBackendProxy.Values["agent_name_1"]);
	}

	private static int Count(string text, string value)
	{
		return text.Split(value).Length - 1;
	}

	private static string FindDirectory()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string[] buffer = new string[5];
			buffer[0] = directoryInfo.FullName;
			buffer[1] = "src";
			buffer[2] = "ZzzOd.Gui";
			buffer[3] = "Pages";
			buffer[4] = "ApplicationSettings";
			string text = Path.Combine(buffer);
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		throw new DirectoryNotFoundException("未找到应用设置目录。");
	}
}
