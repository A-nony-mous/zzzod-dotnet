using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Services.RunIntent;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class OperationDebugAxamlPageTests
{
	public class RecordingBackendProxy : DispatchProxy
	{
		private readonly Channel<ZzzBackendEvent> _events = Channel.CreateUnbounded<ZzzBackendEvent>();

		public string RunRoot { get; set; } = string.Empty;

		public List<ZzzSaveConfigScopeRequest> Requests { get; } = new List<ZzzSaveConfigScopeRequest>();

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			ArgumentNullException.ThrowIfNull(targetMethod, "targetMethod");
			string name = targetMethod.Name;
			if (1 == 0)
			{
			}
			object result;
			switch (name)
			{
			case "GetHealth":
				result = ZzzBackendResult<ZzzHealthDto>.Ok(new ZzzHealthDto(ZzzHostMode.Gui, "1.0.0", RunRoot, ApiEnabled: false, ContextReady: true, 7));
				break;
			case "GetCurrentInstance":
				result = ZzzBackendResult<ZzzInstanceDto>.Ok(new ZzzInstanceDto(7, "实例 07", Active: true, Path.Combine(RunRoot, "config", "07")));
				break;
			case "GetConfigScope":
				if (args == null || args.Length < 1 || !(args[0] is string scope))
				{
					goto default;
				}
				result = ReadScope(scope);
				break;
			case "SaveConfigScope":
				if (args == null || args.Length != 1 || !(args[0] is ZzzSaveConfigScopeRequest request))
				{
					goto default;
				}
				result = SaveScope(request);
				break;
			case "GetCurrentRun":
				result = ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(ZzzRunState.Idle));
				break;
			case "GetRecentLogs":
				result = ZzzBackendResult<IReadOnlyList<ZzzLogEntryDto>>.Ok(Array.Empty<ZzzLogEntryDto>());
				break;
			case "SubscribeEvents":
				result = _events.Reader;
				break;
			case "UnsubscribeEvents":
				result = null;
				break;
			default:
				throw new NotSupportedException(targetMethod.Name);
			}
			if (1 == 0)
			{
			}
			return result;
		}

		private ZzzBackendResult<ZzzConfigScopeValuesDto> ReadScope(string scope)
		{
			if (1 == 0)
			{
			}
			Dictionary<string, object> dictionary = scope switch
			{
				"operation-debug" => new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["operation_template"] = "sub/beta",
					["repeat_enabled"] = true
				}, 
				"battle-assistant" => new Dictionary<string, object>(StringComparer.Ordinal) { ["control_method"] = "ds4" }, 
				"env" => new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["key_start_running"] = "f9",
					["key_stop_running"] = "f10"
				}, 
				_ => throw new NotSupportedException(scope), 
			};
			if (1 == 0)
			{
			}
			Dictionary<string, object> values = dictionary;
			return Snapshot(scope, values);
		}

		private ZzzBackendResult<ZzzConfigScopeValuesDto> SaveScope(ZzzSaveConfigScopeRequest request)
		{
			Requests.Add(request);
			return Snapshot(request.Scope, new Dictionary<string, object>(request.Values, StringComparer.Ordinal));
		}

		private static ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot(string scope, IReadOnlyDictionary<string, object?> values)
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(new ZzzConfigScopeDescriptorDto(scope, scope, InstanceBound: true, scope == "operation-debug", Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>()), 7, (scope == "operation-debug") ? "one_dragon" : null, values));
		}
	}

	[Fact]
	public void PageReadsRecursiveRealFilesDeletesOnlyNormalYamlAndSavesCurrentInstance()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-operation-debug-page-tests", Guid.NewGuid().ToString("N"));
		string configDirectory = Path.Combine(text, "config", "auto_battle_operation");
		Directory.CreateDirectory(Path.Combine(configDirectory, "sub"));
		File.WriteAllText(Path.Combine(configDirectory, "alpha.yml"), "operations: []");
		File.WriteAllText(Path.Combine(configDirectory, "alpha.sample.yml"), "operations: []");
		File.WriteAllText(Path.Combine(configDirectory, "sub", "beta.sample.yml"), "operations: []");
		try
		{
			IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
			RecordingBackendProxy recordingBackendProxy = (RecordingBackendProxy)backend;
			recordingBackendProxy.RunRoot = text;
			GuiParityAndFacadeTests.RunOnUiThread(delegate
			{
				ZzzOd.Gui.Views.FrontierPages.DevTools.FrontierOperationDebugPage zzzOperationDebugAxamlPage = new ZzzOd.Gui.Views.FrontierPages.DevTools.FrontierOperationDebugPage(backend, new ZzzGuiRunIntentService());
				zzzOperationDebugAxamlPage.ReloadForTest();
				Assert.Equal(7, zzzOperationDebugAxamlPage.ActiveInstanceIndex);
				Assert.Equal(new string[2] { "alpha", "sub/beta" }, zzzOperationDebugAxamlPage.OperationTemplates);
				Assert.Equal("sub/beta", zzzOperationDebugAxamlPage.SelectedOperationTemplate);
				Assert.True(zzzOperationDebugAxamlPage.RepeatEnabled);
				Assert.Equal("ds4", zzzOperationDebugAxamlPage.ControlMethod);
				Assert.True(zzzOperationDebugAxamlPage.SaveOperationTemplateForTest("alpha"));
				Assert.True(zzzOperationDebugAxamlPage.SaveRepeatForTest(value: false));
				Assert.True(zzzOperationDebugAxamlPage.SaveControlMethodForTest("keyboard"));
				Assert.True(zzzOperationDebugAxamlPage.DeleteTemplateForTest("alpha"));
				Assert.False(File.Exists(Path.Combine(configDirectory, "alpha.yml")));
				Assert.True(File.Exists(Path.Combine(configDirectory, "alpha.sample.yml")));
				Assert.False(zzzOperationDebugAxamlPage.DeleteTemplateForTest("sub/beta"));
				Assert.True(File.Exists(Path.Combine(configDirectory, "sub", "beta.sample.yml")));
				Assert.False(zzzOperationDebugAxamlPage.DeleteTemplateForTest("../outside"));
			});
			Assert.Contains((IEnumerable<ZzzSaveConfigScopeRequest>)recordingBackendProxy.Requests, (Predicate<ZzzSaveConfigScopeRequest>)((ZzzSaveConfigScopeRequest request) => request.Scope == "operation-debug" && request.InstanceIndex == 7 && request.GroupId == "one_dragon" && request.Values.TryGetValue("operation_template", out object value) && object.Equals(value, "alpha")));
			Assert.Contains((IEnumerable<ZzzSaveConfigScopeRequest>)recordingBackendProxy.Requests, (Predicate<ZzzSaveConfigScopeRequest>)((ZzzSaveConfigScopeRequest request) => request.Scope == "operation-debug" && request.InstanceIndex == 7 && request.GroupId == "one_dragon" && request.Values.TryGetValue("repeat_enabled", out object value) && object.Equals(value, false)));
			Assert.Contains((IEnumerable<ZzzSaveConfigScopeRequest>)recordingBackendProxy.Requests, (Predicate<ZzzSaveConfigScopeRequest>)((ZzzSaveConfigScopeRequest request) => request.Scope == "battle-assistant" && request.InstanceIndex == 7 && request.GroupId == null && request.Values.TryGetValue("control_method", out object value) && object.Equals(value, "keyboard")));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

}
