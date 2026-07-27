using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;

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
	public void ScreenshotMethodOptionsExposeOnlyImplementedBackends()
	{
		string root = FindGuiFile();
		string frontierPage = File.ReadAllText(Path.Combine(root, "Views", "FrontierPages", "Settings", "FrontierEnvironmentSettingsPage.axaml.cs"));
		string source = frontierPage;
		Assert.Contains("Windows Graphics Capture", source, StringComparison.Ordinal);
		Assert.Contains("print_window", source, StringComparison.Ordinal);
		Assert.Contains("bitblt", source, StringComparison.Ordinal);
		Assert.True(source.IndexOf("new(\"自动\", \"auto\")", StringComparison.Ordinal) < source.IndexOf("new(\"Windows Graphics Capture\", \"wgc\")", StringComparison.Ordinal));
		Assert.True(source.IndexOf("new(\"Windows Graphics Capture\", \"wgc\")", StringComparison.Ordinal) < source.IndexOf("new(\"BitBlt\", \"bitblt\")", StringComparison.Ordinal));
		Assert.True(source.IndexOf("new(\"BitBlt\", \"bitblt\")", StringComparison.Ordinal) < source.IndexOf("new(\"Print Window\", \"print_window\")", StringComparison.Ordinal));
		Assert.DoesNotContain("new(\"MSS\"", source, StringComparison.Ordinal);
		Assert.DoesNotContain("new(\"PIL\"", source, StringComparison.Ordinal);
		Assert.DoesNotContain("DWM Shared Surface", source, StringComparison.Ordinal);
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
				ZzzOd.Gui.Views.FrontierPages.Settings.FrontierEnvironmentSettingsPage zzzEnvironmentSettingsAxamlPage = new ZzzOd.Gui.Views.FrontierPages.Settings.FrontierEnvironmentSettingsPage(backend);
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
			string root = Path.Combine(directoryInfo.FullName, "src", "ZzzOd.Gui");
			if (relativeSegments.Length == 0 && Directory.Exists(root))
			{
				return root;
			}
			string text = Path.Combine(root, Path.Combine(relativeSegments));
			if (File.Exists(text))
			{
				return text;
			}
		}
		throw new DirectoryNotFoundException("未找到 ZzzOd.Gui 源码目录。");
	}
}
