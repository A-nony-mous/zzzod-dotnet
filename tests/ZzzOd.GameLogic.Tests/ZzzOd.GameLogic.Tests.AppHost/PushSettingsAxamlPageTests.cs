using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Notifications;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class PushSettingsAxamlPageTests
{
	public class RecordingBackendProxy : DispatchProxy
	{
		private static readonly IReadOnlyDictionary<string, ZzzConfigScopeDescriptorDto> Descriptors = new Dictionary<string, ZzzConfigScopeDescriptorDto>(StringComparer.Ordinal)
		{
			["notify"] = new ZzzConfigScopeDescriptorDto("notify", "通知", InstanceBound: true, GroupBound: false, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>()),
			["push"] = new ZzzConfigScopeDescriptorDto("push", "推送", InstanceBound: false, GroupBound: false, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>()),
			["env"] = new ZzzConfigScopeDescriptorDto("env", "环境", InstanceBound: false, GroupBound: false, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>())
		};

		public Dictionary<string, Dictionary<string, object?>> Scopes { get; } = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal)
		{
			["notify"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["title"] = "一条龙运行通知" },
			["push"] = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["send_image"] = true,
				["proxy"] = "NONE",
				["smtp_server"] = "smtp.example.invalid:465",
				["smtp_ssl"] = "true",
				["smtp_starttls"] = "false",
				["smtp_email"] = "sender@example.invalid",
				["smtp_password"] = "secret",
				["smtp_name"] = "ZZZOD",
				["webhook_url"] = "https://example.invalid/hook",
				["webhook_method"] = "POST",
				["webhook_content_type"] = "application/json",
				["webhook_headers"] = "{\"X-Test\":\"1\"}",
				["webhook_body"] = "{\"content\":\"$content\"}",
				["serverchan_sendkey"] = string.Empty,
				["qywx_key"] = string.Empty
			},
			["env"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["personal_proxy"] = string.Empty }
		};

		public List<ZzzSaveConfigScopeRequest> Requests { get; } = new List<ZzzSaveConfigScopeRequest>();

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			ArgumentNullException.ThrowIfNull(targetMethod, "targetMethod");
			if (targetMethod.Name == "GetConfigScope" && args != null && args.Length >= 1 && args[0] is string scope)
			{
				return Snapshot(scope);
			}
			if (targetMethod.Name == "SaveConfigScope" && args != null && args.Length == 1 && args[0] is ZzzSaveConfigScopeRequest zzzSaveConfigScopeRequest)
			{
				Requests.Add(zzzSaveConfigScopeRequest);
				foreach (var (key, value) in zzzSaveConfigScopeRequest.Values)
				{
					Scopes[zzzSaveConfigScopeRequest.Scope][key] = value;
				}
				return Snapshot(zzzSaveConfigScopeRequest.Scope);
			}
			throw new NotSupportedException(targetMethod.Name);
		}

		private ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot(string scope)
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(Descriptors[scope], (scope == "notify") ? new int?(0) : ((int?)null), null, new Dictionary<string, object>(Scopes[scope], StringComparer.Ordinal)));
		}
	}

	[Fact]
	public void PageReadsRealScopesWritesThroughAndGeneratesCurlLocally()
	{
		IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
		RecordingBackendProxy recordingBackendProxy = (RecordingBackendProxy)backend;
		GuiParityAndFacadeTests.RunOnUiThread(delegate
		{
			ZzzPushNotificationService pushService = new ZzzPushNotificationService(new ZzzRunRoot(Path.GetTempPath()));
			ZzzOd.Gui.Views.FrontierPages.Settings.FrontierPushSettingsPage zzzPushSettingsAxamlPage = new ZzzOd.Gui.Views.FrontierPages.Settings.FrontierPushSettingsPage(backend, pushService);
			zzzPushSettingsAxamlPage.OnPageShown();
			Assert.Equal(24, zzzPushSettingsAxamlPage.ChannelsForTest.Count);
			Assert.Contains((IEnumerable<ZzzPushChannelDescriptor>)zzzPushSettingsAxamlPage.ChannelsForTest, (Predicate<ZzzPushChannelDescriptor>)((ZzzPushChannelDescriptor channel) => channel.ChannelId == "FS" && channel.Fields.Count == 5));
			Assert.Contains((IEnumerable<ZzzPushChannelDescriptor>)zzzPushSettingsAxamlPage.ChannelsForTest, (Predicate<ZzzPushChannelDescriptor>)((ZzzPushChannelDescriptor channel) => channel.ChannelId == "WEBHOOK" && channel.Fields.Any((ZzzPushFieldDescriptor field) => field.Key == "webhook_body")));
			string actualString = zzzPushSettingsAxamlPage.GenerateWebhookCurlForTest("pwsh");
			Assert.Contains("curl.exe -X POST", actualString, StringComparison.Ordinal);
			Assert.Contains("https://example.invalid/hook", actualString, StringComparison.Ordinal);
			Assert.Contains("X-Test: 1", actualString, StringComparison.Ordinal);
			Assert.Contains("$content", actualString, StringComparison.Ordinal);
			zzzPushSettingsAxamlPage.SaveValueForTest("notify", "title", "运行完成");
			zzzPushSettingsAxamlPage.SaveValueForTest("push", "send_image", false);
			zzzPushSettingsAxamlPage.SaveValueForTest("env", "personal_proxy", "http://127.0.0.1:8080");
		});
		Assert.Equal("运行完成", recordingBackendProxy.Scopes["notify"]["title"]);
		Assert.Equal(false, recordingBackendProxy.Scopes["push"]["send_image"]);
		Assert.Equal("http://127.0.0.1:8080", recordingBackendProxy.Scopes["env"]["personal_proxy"]);
		Assert.Contains((IEnumerable<ZzzSaveConfigScopeRequest>)recordingBackendProxy.Requests, (Predicate<ZzzSaveConfigScopeRequest>)((ZzzSaveConfigScopeRequest request) => request.Scope == "notify"));
		Assert.Contains((IEnumerable<ZzzSaveConfigScopeRequest>)recordingBackendProxy.Requests, (Predicate<ZzzSaveConfigScopeRequest>)((ZzzSaveConfigScopeRequest request) => request.Scope == "push"));
		Assert.Contains((IEnumerable<ZzzSaveConfigScopeRequest>)recordingBackendProxy.Requests, (Predicate<ZzzSaveConfigScopeRequest>)((ZzzSaveConfigScopeRequest request) => request.Scope == "env"));
	}

}
