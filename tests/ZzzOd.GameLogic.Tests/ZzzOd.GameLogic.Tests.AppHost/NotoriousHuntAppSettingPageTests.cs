using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.ChargePlan;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 恶名狩猎应用设置页 AXAML 和真实配置审计。
/// </summary>
public sealed class NotoriousHuntAppSettingPageTests
{
	/// <summary>
	/// 页面应保持 BaselineParity 控件顺序、Fluent 组件和真实数据调用。
	/// </summary>
	[Fact]
	public void PageUsesAxamlFluentControlsAndRealCatalogs()
	{
		string path = FindDirectory();
		string text = File.ReadAllText(Path.Combine(path, "ZzzNotoriousHuntAppSettingPage.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzNotoriousHuntAppSettingPage.axaml.cs"));
		AssertOrder(text, "恶名狩猎（周期挑战）开始日", "循环执行", "<ItemsControl x:Name=\"PlanList\"", "Content=\"新增\"");
		AssertOrder(text, "MissionTypeOptions", "LevelOptions", "TeamOptions", "AutoBattleOptions", "BuffOptions", "已运行次数", "计划次数", "置顶", "删除");
		Assert.Contains("fa:FASettingsExpanderItem", text, StringComparison.Ordinal);
		Assert.Contains("fa:FAComboBox", text, StringComparison.Ordinal);
		Assert.Contains("fa:FAContentDialog", text, StringComparison.Ordinal);
		Assert.Contains("GetChargePlanCatalog()", actualString, StringComparison.Ordinal);
		Assert.Contains("GetConfigScope(", actualString, StringComparison.Ordinal);
		Assert.Contains("SaveConfigScope", actualString, StringComparison.Ordinal);
		Assert.Contains("catalogResult.Value.Teams", actualString, StringComparison.Ordinal);
		Assert.Contains("catalogResult.Value.AutoBattleConfigs", actualString, StringComparison.Ordinal);
		Assert.Contains("DataFormat.CreateStringApplicationFormat", actualString, StringComparison.Ordinal);
		Assert.Contains("Math.Abs(current.X - _dragStart.X) + Math.Abs(current.Y - _dragStart.Y) < 10", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("DefaultPlan", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("PageModel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("Python", text, StringComparison.Ordinal);
	}

	/// <summary>
	/// 恶名狩猎计划应写入当前实例和应用组的 BaselineParity 路径，并能重新读取。
	/// </summary>
	[Fact]
	public void ScopePersistsPlanListToCurrentInstanceAndGroup()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-notorious-hunt-setting", Guid.NewGuid().ToString("N"));
		try
		{
			ZzzConfigScopeService zzzConfigScopeService = new ZzzConfigScopeService(text);
			int num = 1;
			List<ChargePlanItem> list = new List<ChargePlanItem>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = new ChargePlanItem
			{
				TabName = "训练",
				CategoryName = "恶名狩猎",
				MissionTypeName = "初生死路屠夫",
				MissionName = null,
				Level = "等级Lv.65",
				PredefinedTeamIndex = -1,
				AutoBattleConfig = "用户战斗配置",
				RunTimes = 1,
				PlanTimes = 3,
				NotoriousHuntBuffNum = 2,
				PlanId = "notorious-plan-1"
			};
			List<ChargePlanItem> value = list;
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = zzzConfigScopeService.Save(new ZzzSaveConfigScopeRequest("notorious-hunt", new Dictionary<string, object>
			{
				["weekly_challenge_start_weekday"] = 4,
				["loop"] = false,
				["plan_list"] = value
			}, 3, "weekly"));
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			string[] buffer = new string[5];
			buffer[0] = text;
			buffer[1] = "config";
			buffer[2] = "03";
			buffer[3] = "weekly";
			buffer[4] = "notorious_hunt.yml";
			string path = Path.Combine(buffer);
			Assert.True(File.Exists(path));
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult2 = zzzConfigScopeService.Read("notorious-hunt", 3, "weekly");
			Assert.True(zzzBackendResult2.Success, zzzBackendResult2.Error);
			Assert.Equal(4, zzzBackendResult2.Value.Values["weekly_challenge_start_weekday"]);
			Assert.Equal(false, zzzBackendResult2.Value.Values["loop"]);
			ChargePlanItem chargePlanItem = Assert.Single(Assert.IsType<List<ChargePlanItem>>(zzzBackendResult2.Value.Values["plan_list"]));
			Assert.Equal("初生死路屠夫", chargePlanItem.MissionTypeName);
			Assert.Equal("等级Lv.65", chargePlanItem.Level);
			Assert.Equal(-1, chargePlanItem.PredefinedTeamIndex);
			Assert.Equal("用户战斗配置", chargePlanItem.AutoBattleConfig);
			Assert.Equal(2, chargePlanItem.NotoriousHuntBuffNum);
			Assert.Equal("notorious-plan-1", chargePlanItem.PlanId);
		}
		finally
		{
			if (Directory.Exists(text))
			{
				Directory.Delete(text, recursive: true);
			}
		}
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
