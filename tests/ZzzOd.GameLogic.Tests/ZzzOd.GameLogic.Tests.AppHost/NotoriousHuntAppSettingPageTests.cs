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

}
