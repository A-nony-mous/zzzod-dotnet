using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Pages.Settings;

namespace ZzzOd.GameLogic.Tests.AppHost;

[Collection("Settings environment variables")]
public sealed class SettingsEnvironmentPageTests
{
	public class RecordingBackendProxy : DispatchProxy
	{
		private static readonly ZzzConfigScopeDescriptorDto Descriptor = new ZzzConfigScopeDescriptorDto("env", "脚本环境", InstanceBound: false, GroupBound: false, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>());

		public Dictionary<string, object?> Values { get; } = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["screenshot_method"] = "auto",
			["is_debug"] = false,
			["copy_screenshot"] = true,
			["proxy_type"] = "None",
			["personal_proxy"] = string.Empty,
			["key_start_running"] = "f9",
			["key_stop_running"] = "f10",
			["key_screenshot"] = "f11",
			["key_debug"] = "f12"
		};

		public List<ZzzSaveConfigScopeRequest> Requests { get; } = new List<ZzzSaveConfigScopeRequest>();

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			ArgumentNullException.ThrowIfNull(targetMethod, "targetMethod");
			if (targetMethod.Name == "GetConfigScope")
			{
				return Snapshot();
			}
			if (targetMethod.Name == "SaveConfigScope" && args != null && args.Length == 1 && args[0] is ZzzSaveConfigScopeRequest zzzSaveConfigScopeRequest)
			{
				Requests.Add(zzzSaveConfigScopeRequest);
				foreach (var (key, value) in zzzSaveConfigScopeRequest.Values)
				{
					Values[key] = value;
				}
				return Snapshot();
			}
			throw new NotSupportedException(targetMethod.Name);
		}

		private ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot()
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(Descriptor, null, null, new Dictionary<string, object>(Values, StringComparer.Ordinal)));
		}
	}

	[Fact]
	public void AxamlKeepsApprovedGroupsTextsAndControlsOnly()
	{
		string text = File.ReadAllText(FindGuiFile("Pages", "Settings", "ZzzEnvironmentSettingsPage.axaml"));
		int num = text.IndexOf("Header=\"基础\"", StringComparison.Ordinal);
		int num2 = text.IndexOf("Header=\"网络相关\"", StringComparison.Ordinal);
		int num3 = text.IndexOf("Header=\"脚本按键\"", StringComparison.Ordinal);
		Assert.True(num >= 0 && num < num2 && num2 < num3);
		Assert.Contains("fa:FASettingsExpander", text, StringComparison.Ordinal);
		Assert.Contains("fa:FASettingsExpanderItem", text, StringComparison.Ordinal);
		Assert.Contains("fa:FAComboBox", text, StringComparison.Ordinal);
		Assert.Contains("正常无需开启", text, StringComparison.Ordinal);
		Assert.Contains("按下截图按键时，自动将截图复制到剪贴板", text, StringComparison.Ordinal);
		Assert.Contains("开始、暂停、恢复某个应用", text, StringComparison.Ordinal);
		Assert.Contains("停止正在运行的应用，不能恢复", text, StringComparison.Ordinal);
		Assert.Contains("用于开发、提交bug。会自动对UID打码，保存在 .debug/images/ 文件夹中", text, StringComparison.Ordinal);
		Assert.Contains("用于开发，部分应用开始调试", text, StringComparison.Ordinal);
		Assert.DoesNotContain("Git相关", text, StringComparison.Ordinal);
		Assert.DoesNotContain("代码源", text, StringComparison.Ordinal);
		Assert.DoesNotContain("自动更新", text, StringComparison.Ordinal);
		Assert.DoesNotContain("强制更新", text, StringComparison.Ordinal);
		Assert.DoesNotContain("Python下载源", text, StringComparison.Ordinal);
		Assert.DoesNotContain("Pip源", text, StringComparison.Ordinal);
		Assert.DoesNotContain("免费代理", text, StringComparison.Ordinal);
		Assert.DoesNotContain("GitHub 代理", text, StringComparison.Ordinal);
		Assert.DoesNotContain("已适配", text, StringComparison.Ordinal);
		Assert.DoesNotContain("已从 .NET GUI 移除", text, StringComparison.Ordinal);
	}

	[Fact]
	public void ProxyChangesWriteEnvScopeAndUpdateCurrentProcessProxy()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("HTTP_PROXY");
		string environmentVariable2 = Environment.GetEnvironmentVariable("HTTPS_PROXY");
		IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
		RecordingBackendProxy recordingBackendProxy = (RecordingBackendProxy)backend;
		try
		{
			GuiParityAndFacadeTests.RunOnUiThread(delegate
			{
				ZzzEnvironmentSettingsAxamlPage zzzEnvironmentSettingsAxamlPage = new ZzzEnvironmentSettingsAxamlPage(backend);
				zzzEnvironmentSettingsAxamlPage.OnPageShown();
				Assert.Equal("None", zzzEnvironmentSettingsAxamlPage.SelectedProxyType);
				Assert.False(zzzEnvironmentSettingsAxamlPage.PersonalProxyVisible);
				zzzEnvironmentSettingsAxamlPage.SaveStringForTest("personal_proxy", "http://127.0.0.1:8080");
				zzzEnvironmentSettingsAxamlPage.SaveStringForTest("proxy_type", "personal");
				Assert.Equal("personal", zzzEnvironmentSettingsAxamlPage.SelectedProxyType);
				Assert.True(zzzEnvironmentSettingsAxamlPage.PersonalProxyVisible);
				Assert.Equal("http://127.0.0.1:8080", Environment.GetEnvironmentVariable("HTTP_PROXY"));
				Assert.Equal("http://127.0.0.1:8080", Environment.GetEnvironmentVariable("HTTPS_PROXY"));
				zzzEnvironmentSettingsAxamlPage.SaveStringForTest("screenshot_method", "bitblt");
				zzzEnvironmentSettingsAxamlPage.SaveStringForTest("proxy_type", "None");
				Assert.False(zzzEnvironmentSettingsAxamlPage.PersonalProxyVisible);
				Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HTTP_PROXY")));
				Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HTTPS_PROXY")));
			});
			Assert.Equal("http://127.0.0.1:8080", recordingBackendProxy.Values["personal_proxy"]);
			Assert.Equal("None", recordingBackendProxy.Values["proxy_type"]);
			Assert.Equal("bitblt", recordingBackendProxy.Values["screenshot_method"]);
			Assert.All(recordingBackendProxy.Requests, delegate(ZzzSaveConfigScopeRequest request)
			{
				Assert.Equal("env", request.Scope);
			});
		}
		finally
		{
			Environment.SetEnvironmentVariable("HTTP_PROXY", environmentVariable);
			Environment.SetEnvironmentVariable("HTTPS_PROXY", environmentVariable2);
		}
	}

	private static string FindGuiFile(params string[] relativeSegments)
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string text = Path.Combine(directoryInfo.FullName, "src", "ZzzOd.Gui", Path.Combine(relativeSegments));
			if (File.Exists(text))
			{
				return text;
			}
		}
		throw new DirectoryNotFoundException("未找到 ZzzOd.Gui 源码目录。");
	}
}
