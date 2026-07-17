using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Pages.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class CoffeeAppSettingPageTests
{
	public class RecordingBackendProxy : DispatchProxy
	{
		public Dictionary<string, object?> CoffeeValues { get; } = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["transport_point"] = "澄辉坪 - 汀曼咖啡",
			["choose_way"] = "优先体力计划",
			["challenge_way"] = "全都挑战",
			["card_num"] = "1",
			["auto_battle"] = "全配队通用",
			["predefined_team_idx"] = -1,
			["run_charge_plan_afterwards"] = false
		};

		public List<(string Scope, int? InstanceIndex, string? GroupId)> Reads { get; } = new List<(string, int?, string)>();

		public List<ZzzSaveConfigScopeRequest> Requests { get; } = new List<ZzzSaveConfigScopeRequest>();

		public int CatalogReads { get; private set; }

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			ArgumentNullException.ThrowIfNull(targetMethod, "targetMethod");
			if (targetMethod.Name == "GetConfigScope" && args != null && args.Length == 3 && args[0] is string text)
			{
				int? num = args[1] as int?;
				string text2 = args[2] as string;
				Reads.Add((text, num, text2));
				return (text == "team") ? TeamSnapshot(num) : CoffeeSnapshot(num, text2);
			}
			if (targetMethod.Name == "SaveConfigScope" && args != null && args.Length == 1 && args[0] is ZzzSaveConfigScopeRequest zzzSaveConfigScopeRequest)
			{
				Requests.Add(zzzSaveConfigScopeRequest);
				foreach (var (key, value) in zzzSaveConfigScopeRequest.Values)
				{
					CoffeeValues[key] = value;
				}
				return CoffeeSnapshot(zzzSaveConfigScopeRequest.InstanceIndex, zzzSaveConfigScopeRequest.GroupId);
			}
			if (targetMethod.Name == "GetBattleAssistantConfigCatalog")
			{
				CatalogReads++;
				return ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto>.Ok(new ZzzBattleAssistantConfigCatalogDto(new string[2] { "全配队通用", "安比模板" }, Array.Empty<string>()));
			}
			throw new NotSupportedException(targetMethod.Name);
		}

		private ZzzBackendResult<ZzzConfigScopeValuesDto> CoffeeSnapshot(int? instanceIndex, string? groupId)
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(new ZzzConfigScopeDescriptorDto("coffee", "咖啡计划", InstanceBound: true, GroupBound: true, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>()), instanceIndex, groupId, new Dictionary<string, object>(CoffeeValues, StringComparer.Ordinal)));
		}

		private static ZzzBackendResult<ZzzConfigScopeValuesDto> TeamSnapshot(int? instanceIndex)
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(new ZzzConfigScopeDescriptorDto("team", "预备编队", InstanceBound: true, GroupBound: false, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>()), instanceIndex, null, new Dictionary<string, object> { ["team_list"] = new List<PredefinedTeamInfo>
			{
				new PredefinedTeamInfo(0, "编队一", "全配队通用", new List<string>()),
				new PredefinedTeamInfo(1, "编队二", "安比模板", new List<string>())
			} }));
		}
	}

	[Fact]
	public void PageUsesAxamlFluentControlsAndPythonTexts()
	{
		string path = FindDirectory();
		string text = File.ReadAllText(Path.Combine(path, "ZzzCoffeeAppSettingPage.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzCoffeeAppSettingPage.axaml.cs"));
		AssertOrder(text, "传送地点", "咖啡选择", "喝后挑战", "体力计划外的数量", "预备编队", "自动战斗", "结束后运行体力计划");
		Assert.Contains("咖啡店在体力计划后运行可开启", text, StringComparison.Ordinal);
		Assert.Contains("fa:SettingsExpanderItem", text, StringComparison.Ordinal);
		Assert.Contains("fa:FAComboBox", text, StringComparison.Ordinal);
		Assert.Contains("ToggleSwitch", text, StringComparison.Ordinal);
		Assert.Contains("六分街 - 咖啡店", actualString, StringComparison.Ordinal);
		Assert.Contains("澄辉坪 - 汀曼咖啡", actualString, StringComparison.Ordinal);
		Assert.Contains("CoffeeChooseWay.PlanPriority", actualString, StringComparison.Ordinal);
		Assert.Contains("CoffeeChallengeWay.Options", actualString, StringComparison.Ordinal);
		Assert.Contains("CoffeeCardNum.Default", actualString, StringComparison.Ordinal);
		Assert.Contains("GetConfigScope(\"team\", _instanceIndex)", actualString, StringComparison.Ordinal);
		Assert.Contains("GetBattleAssistantConfigCatalog", actualString, StringComparison.Ordinal);
		Assert.Contains("value == -1", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("PageModel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("Python", text, StringComparison.Ordinal);
	}

	[Fact]
	public void PageReadsRealTeamAndAutoBattleCatalogAndWritesRequestedScope()
	{
		IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
		RecordingBackendProxy recordingBackendProxy = (RecordingBackendProxy)backend;
		GuiParityAndFacadeTests.RunOnUiThread(delegate
		{
			ZzzCoffeeAppSettingPage zzzCoffeeAppSettingPage = new ZzzCoffeeAppSettingPage(backend, 4, "daily");
			Assert.True(zzzCoffeeAppSettingPage.AutoBattleVisible);
			zzzCoffeeAppSettingPage.SaveForTest("choose_way", "浓缩咖啡");
			zzzCoffeeAppSettingPage.SaveForTest("predefined_team_idx", 2);
		});
		Assert.Contains<(string, int?, string)>(recordingBackendProxy.Reads, delegate((string Scope, int? InstanceIndex, string GroupId) read)
		{
			(string, int?, string) tuple = read;
			return tuple.Item1 == "coffee" && tuple.Item2 == 4 && tuple.Item3 == "daily";
		});
		Assert.Contains<(string, int?, string)>(recordingBackendProxy.Reads, delegate((string Scope, int? InstanceIndex, string GroupId) read)
		{
			(string, int?, string) tuple = read;
			return tuple.Item1 == "team" && tuple.Item2 == 4 && tuple.Item3 == null;
		});
		Assert.Equal(1, recordingBackendProxy.CatalogReads);
		Assert.All(recordingBackendProxy.Requests, delegate(ZzzSaveConfigScopeRequest request)
		{
			Assert.Equal("coffee", request.Scope);
			Assert.Equal(4, request.InstanceIndex);
			Assert.Equal("daily", request.GroupId);
		});
		Assert.Equal("浓缩咖啡", recordingBackendProxy.CoffeeValues["choose_way"]);
		Assert.Equal(2, recordingBackendProxy.CoffeeValues["predefined_team_idx"]);
	}

	private static void AssertOrder(string text, params string[] markers)
	{
		int num = -1;
		foreach (string text2 in markers)
		{
			int num2 = text.IndexOf(text2, StringComparison.Ordinal);
			Assert.True(num2 > num, "未按顺序找到 " + text2 + "。");
			num = num2;
		}
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
