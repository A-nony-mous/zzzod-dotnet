using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.GameData;
using ZzzOd.Gui.Pages.OneDragon;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 预备编队页面的 BaselineParity 对等合同测试。
/// </summary>
public sealed class PredefinedTeamPageParityTests
{
	/// <summary>配置层按 BaselineParity 语义补足二十队。</summary>
	[Fact]
	public void TeamConfig_NormalizesToTwentyPythonCompatibleTeams()
	{
		TeamConfig teamConfig = new TeamConfig();
		int num = 1;
		List<PredefinedTeamInfo> list = new List<PredefinedTeamInfo>(num);
		CollectionsMarshal.SetCount(list, num);
		ref PredefinedTeamInfo reference = ref CollectionsMarshal.AsSpan(list)[0];
		int num2 = 1;
		List<string> list2 = new List<string>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = "anby";
		reference = new PredefinedTeamInfo(0, "主队", "主队配置", list2);
		teamConfig.TeamList = list;
		TeamConfig teamConfig2 = teamConfig;
		Assert.Equal(20, teamConfig2.TeamList.Count);
		num = 3;
		List<string> list3 = new List<string>(num);
		CollectionsMarshal.SetCount(list3, num);
		Span<string> span = CollectionsMarshal.AsSpan(list3);
		span[0] = "anby";
		span[1] = "unknown";
		span[2] = "unknown";
		Assert.Equal<List<string>>(list3, teamConfig2.TeamList[0].AgentIdList);
		Assert.Equal("编队20", teamConfig2.TeamList[19].Name);
		Assert.Equal("全配队通用", teamConfig2.TeamList[19].AutoBattle);
		num = 3;
		List<string> list4 = new List<string>(num);
		CollectionsMarshal.SetCount(list4, num);
		Span<string> span2 = CollectionsMarshal.AsSpan(list4);
		span2[0] = "unknown";
		span2[1] = "unknown";
		span2[2] = "unknown";
		Assert.Equal<List<string>>(list4, teamConfig2.TeamList[19].AgentIdList);
	}

	/// <summary>代理人选项来自生产 AgentEnum 并保持顺序。</summary>
	[Fact]
	public void AgentOptions_ComeFromProductionAgentEnumInPythonOrder()
	{
		IReadOnlyList<ZzzPredefinedTeamOption> readOnlyList = ZzzPredefinedTeamPage.CreateAgentOptions();
		Assert.Equal(AgentEnum.Values.Count + 1, readOnlyList.Count);
		Assert.Equal<(string, string)>(("代理人", "unknown"), (readOnlyList[0].Label, readOnlyList[0].Value));
		Assert.Equal(AgentEnum.Values.Select((AgentEnum item) => item.Value.AgentId), from option in readOnlyList.Skip(1)
			select option.Value);
		Assert.Equal(AgentEnum.Values.Select((AgentEnum item) => item.Value.AgentName), from option in readOnlyList.Skip(1)
			select option.Label);
	}

	/// <summary>编队名称使用 BaselineParity 的中英文宽度限制。</summary>
	[Theory]
	[InlineData(new object[] { "中文七字刚刚好", true })]
	[InlineData(new object[] { "中文八个字会超过限制", false })]
	[InlineData(new object[] { "abcdefghijklmn", true })]
	[InlineData(new object[] { "abcdefghijklmno", false })]
	[InlineData(new object[] { "中文abc", true })]
	public void TeamNameWidth_MatchesPythonValidator(string value, bool expected)
	{
		Assert.Equal(expected, ZzzPredefinedTeamPage.IsTeamNameWithinLimit(value));
	}

	/// <summary>目录缺项时保留配置文件中的原始值。</summary>
	[Fact]
	public void RowModel_PreservesValuesMissingFromCurrentCatalog()
	{
		int num = 3;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "future_agent";
		span[1] = "anby";
		span[2] = "unknown";
		ZzzPredefinedTeamRowModel zzzPredefinedTeamRowModel = new ZzzPredefinedTeamRowModel(new PredefinedTeamInfo(0, "主队", "已删除配置", list), Array.Empty<ZzzPredefinedTeamOption>(), ZzzPredefinedTeamPage.CreateAgentOptions());
		zzzPredefinedTeamRowModel.Name = "主力队";
		Assert.True(zzzPredefinedTeamRowModel.HasChanges);
		Assert.Equal("已删除配置", zzzPredefinedTeamRowModel.AutoBattleValue);
		Assert.Equal("future_agent", zzzPredefinedTeamRowModel.Agent1Value);
		Assert.Equal("anby", zzzPredefinedTeamRowModel.Agent2Value);
		Assert.Equal("unknown", zzzPredefinedTeamRowModel.Agent3Value);
	}

	/// <summary>AXAML 使用 Fluent 控件且不增加代理人编号标签。</summary>
	[Fact]
	public void Axaml_UsesFluentControlsWithoutNumberedAgentLabels()
	{
		string text = FindRepositoryRoot();
		string[] buffer = new string[6];
		buffer[0] = text;
		buffer[1] = "src";
		buffer[2] = "ZzzOd.Gui";
		buffer[3] = "Pages";
		buffer[4] = "OneDragon";
		buffer[5] = "ZzzPredefinedTeamPage.axaml";
		string actualString = File.ReadAllText(Path.Combine(buffer));
		Assert.Contains("fa:FASettingsExpanderItem", actualString, StringComparison.Ordinal);
		Assert.Contains("fa:FAComboBox", actualString, StringComparison.Ordinal);
		Assert.Contains("战斗配置", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("代理人 1", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("代理人 2", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("代理人 3", actualString, StringComparison.Ordinal);
	}

	private static string FindRepositoryRoot()
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory);
		while (directoryInfo != null && !File.Exists(Path.Combine(directoryInfo.FullName, "ZzzOneDragon.slnx")))
		{
			directoryInfo = directoryInfo.Parent;
		}
		return directoryInfo?.FullName ?? throw new DirectoryNotFoundException("找不到 ZzzOneDragon.slnx。");
	}
}
