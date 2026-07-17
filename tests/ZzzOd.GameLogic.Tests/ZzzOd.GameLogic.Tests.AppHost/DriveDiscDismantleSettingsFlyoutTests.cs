using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Pages.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class DriveDiscDismantleSettingsFlyoutTests
{
	public class RecordingBackendProxy : DispatchProxy
	{
		public Dictionary<string, object?> Values { get; } = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["dismantle_level"] = "A及以下",
			["dismantle_abandon"] = false
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
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(new ZzzConfigScopeDescriptorDto(scope, "驱动盘拆解", InstanceBound: true, GroupBound: true, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>()), instanceIndex, groupId, new Dictionary<string, object>(Values, StringComparer.Ordinal)));
		}
	}

	[Fact]
	public void FlyoutUsesAxamlFluentControlsAndPythonTexts()
	{
		string path = FindDirectory();
		string text = File.ReadAllText(Path.Combine(path, "ZzzDriveDiscDismantleSettingsFlyoutContent.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzDriveDiscDismantleSettingsFlyoutContent.axaml.cs"));
		Assert.True(text.IndexOf("拆解等级", StringComparison.Ordinal) < text.IndexOf("全部已弃置", StringComparison.Ordinal));
		Assert.Contains("fa:SettingsExpanderItem", text, StringComparison.Ordinal);
		Assert.Contains("fa:FAComboBox", text, StringComparison.Ordinal);
		Assert.Contains("ToggleSwitch", text, StringComparison.Ordinal);
		Assert.Contains("new ZzzDriveDiscOption(\"B\", \"B\")", actualString, StringComparison.Ordinal);
		Assert.Contains("new ZzzDriveDiscOption(\"A及以下\", \"A及以下\")", actualString, StringComparison.Ordinal);
		Assert.Contains("new ZzzDriveDiscOption(\"S及以下\", \"S及以下\")", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("PageModel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("Python", text, StringComparison.Ordinal);
	}

	[Fact]
	public void FlyoutReadsAndWritesRequestedInstanceAndGroup()
	{
		IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
		RecordingBackendProxy recordingBackendProxy = (RecordingBackendProxy)backend;
		GuiParityAndFacadeTests.RunOnUiThread(delegate
		{
			ZzzDriveDiscDismantleSettingsFlyoutContent zzzDriveDiscDismantleSettingsFlyoutContent = new ZzzDriveDiscDismantleSettingsFlyoutContent(backend, 3, "daily");
			zzzDriveDiscDismantleSettingsFlyoutContent.SaveForTest("dismantle_level", "S及以下");
			zzzDriveDiscDismantleSettingsFlyoutContent.SaveForTest("dismantle_abandon", true);
		});
		Assert.All(recordingBackendProxy.Requests, delegate(ZzzSaveConfigScopeRequest request)
		{
			Assert.Equal("drive-disc-dismantle", request.Scope);
			Assert.Equal(3, request.InstanceIndex);
			Assert.Equal("daily", request.GroupId);
		});
		Assert.Equal("S及以下", recordingBackendProxy.Values["dismantle_level"]);
		Assert.Equal(true, recordingBackendProxy.Values["dismantle_abandon"]);
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
