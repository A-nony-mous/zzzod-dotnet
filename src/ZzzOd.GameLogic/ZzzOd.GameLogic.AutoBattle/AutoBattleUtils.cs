using System;
using System.Collections.Generic;
using System.Threading;
using OneDragon.Core.Operation;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.AutoBattle;

public static class AutoBattleUtils
{
	private static readonly HashSet<string> HighestMovePriorityAgentIds = new HashSet<string> { "anby", "nekomata", "corin", "koleda", "soukaku", "lucy", "qingyi", "piper", "ju_fufu", "dialyn" };

	private static readonly HashSet<string> LowMovePriorityAgentIds = new HashSet<string> { "hoshimi_miyabi", "yixuan", "billy", "ben", "panyinhu", "zhao" };

	public static AgentInfo? GetBestAgentForMoving(TeamInfo? teamInfo)
	{
		if (teamInfo == null || teamInfo.Agents.Count == 0)
		{
			return null;
		}
		AgentInfo result = null;
		int num = 99;
		foreach (AgentInfo item in teamInfo.Snapshot())
		{
			if (item.Agent != null)
			{
				int agentPriority = GetAgentPriority(item.Agent);
				if (agentPriority < num)
				{
					num = agentPriority;
					result = item;
				}
			}
		}
		return result;
	}

	public static int GetAgentPriority(Agent agent)
	{
		if (agent.AgentId == "astra_yao")
		{
			return 5;
		}
		if (HighestMovePriorityAgentIds.Contains(agent.AgentId))
		{
			return 0;
		}
		if (LowMovePriorityAgentIds.Contains(agent.AgentId))
		{
			return 4;
		}
		AgentTypeEnum agentType = agent.AgentType;
		if (1 == 0)
		{
		}
		int result = agentType switch
		{
			AgentTypeEnum.SUPPORT => 1, 
			AgentTypeEnum.DEFENSE => 2, 
			_ => 3, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	public static bool SwitchToBestAgentForMoving(ZContext ctx, float timeoutSeconds = 5f)
	{
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		while ((DateTimeOffset.UtcNow - utcNow).TotalSeconds < (double)timeoutSeconds)
		{
			RefreshAgentContextFromScreenshot(ctx);
			IReadOnlyList<AgentInfo> readOnlyList = ctx.AutoBattleContext.AgentContext.Team.Snapshot();
			if (readOnlyList.Count == 0)
			{
				Thread.Sleep(TimeSpan.FromMilliseconds(200L));
				continue;
			}
			AgentInfo bestAgentForMoving = GetBestAgentForMoving(ctx.AutoBattleContext.AgentContext.Team);
			if (bestAgentForMoving?.Agent == null)
			{
				Thread.Sleep(TimeSpan.FromMilliseconds(200L));
				continue;
			}
			if (readOnlyList[0].Agent?.AgentId == bestAgentForMoving.Agent.AgentId)
			{
				return true;
			}
			ctx.AutoBattleContext.SwitchByName(bestAgentForMoving.Agent.AgentName);
			Thread.Sleep(TimeSpan.FromMilliseconds(200L));
		}
		return false;
	}

	private static void RefreshAgentContextFromScreenshot(ZContext ctx)
	{
		if (ctx.Controller == null)
		{
			return;
		}
		try
		{
			var (dateTimeOffset, mat) = ctx.Controller.Screenshot();
			try
			{
				if (mat != null)
				{
					double screenshotTime = (double)dateTimeOffset.ToUnixTimeMilliseconds() / 1000.0;
					ctx.AutoBattleContext.AgentContext.CheckAgentRelated(mat, screenshotTime);
				}
			}
			finally
			{
				mat?.Dispose();
			}
		}
		catch (InvalidOperationException)
		{
		}
	}

	public static bool CheckBattleEncounterInPeriod(ZContext ctx, float totalCheckSeconds)
	{
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		while (DateTimeOffset.UtcNow - utcNow < TimeSpan.FromSeconds(totalCheckSeconds))
		{
			(DateTimeOffset, Mat)? tuple = ctx.Controller?.Screenshot();
			if (tuple.HasValue)
			{
				(DateTimeOffset, Mat) valueOrDefault = tuple.GetValueOrDefault();
				if (true)
				{
					using Mat screen = valueOrDefault.Item2;
					if (CheckBattleEncounter(ctx, screen, valueOrDefault.Item1))
					{
						return true;
					}
				}
			}
			Thread.Sleep(TimeSpan.FromSeconds(ctx.BattleAssistantConfig.ScreenshotInterval));
		}
		return false;
	}

	/// <summary>
	/// 对齐 BaselineParity LostVoidContext.check_battle_encounter 的单帧战斗检查。
	/// </summary>
	public static bool CheckBattleEncounter(ZContext ctx, Mat? screen, DateTimeOffset? screenshotTimeUtc = null)
	{
		if (screen == null)
		{
			return false;
		}
		double num = (double)(screenshotTimeUtc ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds() / 1000.0;
		bool flag = ctx.AutoBattleContext.AutoOp != null && ctx.AutoBattleContext.IsNormalAttackButtonAvailable(screen);
		if (flag)
		{
			ctx.AutoBattleContext.AgentContext.CheckAgentRelated(screen, num);
			if (HasStateAtTime(ctx.AutoBattleContext.StateRecordService, CommonAgentStateEnum.LIFE_DEDUCTION_31.Value.StateName, num))
			{
				ctx.Logger.Information("迷失之地遭遇战斗判定: Source=HpDeduction, AttackButton=true, ScreenshotTime={ScreenshotTime:F3}", num);
				return true;
			}
			ctx.AutoBattleContext.DodgeContext.CheckDodgeFlash(screen, num);
			if (HasStateAtTime(ctx.AutoBattleContext.StateRecordService, YoloStateEventEnum.DODGE_RED.GetDescription(), num))
			{
				ctx.Logger.Information("迷失之地遭遇战斗判定: Source=RedFlash, AttackButton=true, ScreenshotTime={ScreenshotTime:F3}", num);
				return true;
			}
			if (HasStateAtTime(ctx.AutoBattleContext.StateRecordService, YoloStateEventEnum.DODGE_YELLOW.GetDescription(), num))
			{
				ctx.Logger.Information("迷失之地遭遇战斗判定: Source=YellowFlash, AttackButton=true, ScreenshotTime={ScreenshotTime:F3}", num);
				return true;
			}
		}
		return false;
	}

	public static bool CheckBattleEncounterFromState(AutoBattleStateRecordService stateRecordService, double screenshotTime)
	{
		return HasStateAtTime(stateRecordService, CommonAgentStateEnum.LIFE_DEDUCTION_31.Value.StateName, screenshotTime) || HasStateAtTime(stateRecordService, YoloStateEventEnum.DODGE_RED.GetDescription(), screenshotTime) || HasStateAtTime(stateRecordService, YoloStateEventEnum.DODGE_YELLOW.GetDescription(), screenshotTime);
	}

	private static bool HasStateAtTime(AutoBattleStateRecordService stateRecordService, string stateName, double screenshotTime)
	{
		StateRecorder stateRecorder = stateRecordService.GetStateRecorder(stateName);
		return stateRecorder != null && Math.Abs(stateRecorder.LastRecordTime - screenshotTime) < 0.0001;
	}
}
