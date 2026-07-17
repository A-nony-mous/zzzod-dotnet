using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using YamlDotNet.Serialization;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// BaselineParity 动态推送渠道配置的数据保真测试。
/// </summary>
public sealed class PushConfigPreservationTests
{
	/// <summary>
	/// 保存当前支持字段时应保留未知渠道 key、嵌套数据和未显示字段。
	/// </summary>
	[Fact]
	public void ProductionPushScopePreservesUnknownPythonChannelDataAcrossSaveAndReload()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-push-config-tests", Guid.NewGuid().ToString("N"));
		string text2 = Path.Combine(text, "config");
		string path = Path.Combine(text2, "push.yml");
		Directory.CreateDirectory(text2);
		File.WriteAllText(path, "send_image: true\nproxy: PERSONAL\nwebhook_method: POST\nfs_channel: Lark\nfs_key: retained-key\nntfy_topic: retained-topic\ncustom_channel:\n  endpoint: https://push.example.test\n  headers:\n    Authorization: retained-token\n  targets:\n  - alpha\n  - beta\nunknown_number: 17");
		try
		{
			ZzzConfigScopeService zzzConfigScopeService = new ZzzConfigScopeService(text);
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = zzzConfigScopeService.Read("push", null, null);
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			Assert.True(Assert.IsType<bool>(zzzBackendResult.Value.Values["send_image"]));
			Assert.Equal("PERSONAL", zzzBackendResult.Value.Values["proxy"]);
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult2 = zzzConfigScopeService.Save(new ZzzSaveConfigScopeRequest("push", new Dictionary<string, object>
			{
				["send_image"] = false,
				["webhook_url"] = "https://webhook.example.test/$content"
			}));
			Assert.True(zzzBackendResult2.Success, zzzBackendResult2.Error);
			Assert.False(Assert.IsType<bool>(zzzBackendResult2.Value.Values["send_image"]));
			Assert.Equal("https://webhook.example.test/$content", zzzBackendResult2.Value.Values["webhook_url"]);
			ZzzConfigScopeService zzzConfigScopeService2 = new ZzzConfigScopeService(text);
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult3 = zzzConfigScopeService2.Read("push", null, null);
			Assert.True(zzzBackendResult3.Success, zzzBackendResult3.Error);
			Assert.False(Assert.IsType<bool>(zzzBackendResult3.Value.Values["send_image"]));
			Assert.Equal("PERSONAL", zzzBackendResult3.Value.Values["proxy"]);
			Dictionary<object, object> dictionary = new DeserializerBuilder().Build().Deserialize<Dictionary<object, object>>(File.ReadAllText(path));
			Assert.Equal("Lark", dictionary["fs_channel"]?.ToString());
			Assert.Equal("retained-key", dictionary["fs_key"]?.ToString());
			Assert.Equal("retained-topic", dictionary["ntfy_topic"]?.ToString());
			Assert.Equal("17", dictionary["unknown_number"]?.ToString());
			Dictionary<object, object> dictionary2 = Assert.IsType<Dictionary<object, object>>(dictionary["custom_channel"]);
			Assert.Equal("https://push.example.test", dictionary2["endpoint"]?.ToString());
			Dictionary<object, object> dictionary3 = Assert.IsType<Dictionary<object, object>>(dictionary2["headers"]);
			Assert.Equal("retained-token", dictionary3["Authorization"]?.ToString());
			List<object> source = Assert.IsType<List<object>>(dictionary2["targets"]);
			Assert.Equal<string>((IEnumerable<string>?)new string[2] { "alpha", "beta" }, source.Select((object item) => item.ToString()));
			Assert.Equal("false", dictionary["send_image"]?.ToString(), ignoreCase: true);
			Assert.Equal("https://webhook.example.test/$content", dictionary["webhook_url"]?.ToString());
			Assert.False(dictionary.ContainsKey("smtp_server"));
			Assert.False(dictionary.ContainsKey("serverchan_sendkey"));
			Assert.False(dictionary.ContainsKey("qywx_key"));
		}
		finally
		{
			if (Directory.Exists(text))
			{
				Directory.Delete(text, recursive: true);
			}
		}
	}
}
