using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.Compendium;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 默认式舆防卫战主流程服务。
/// </summary>
public sealed class DefaultShiyuDefenseOperationServices : IShiyuDefenseOperationServices
{
	/// <inheritdoc />
	public Task<OperationResult> TransportAsync(ZContext context)
	{
		return new TransportByCompendium(context, "作战", "式舆防卫战", "剧变节点").ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> WaitForMainScreenAsync(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		if (ScreenUtils.FindArea(context, screen, "式舆防卫战", "前次行动最佳记录") == FindAreaResultEnum.True)
		{
			ClickArea(context, "式舆防卫战", "前次-关闭");
			return Task.FromResult(new OperationResult(IsSuccess: true, "前次行动最佳记录"));
		}
		return Task.FromResult((ScreenUtils.FindArea(context, screen, "式舆防卫战", "前哨档案") == FindAreaResultEnum.True) ? new OperationResult(IsSuccess: true, "前哨档案") : new OperationResult(IsSuccess: false, "等待画面加载"));
	}

	/// <inheritdoc />
	public Task<int?> GetNextNodeIndexAsync(ZContext context, ShiyuDefenseConfig config, ShiyuDefenseRunRecord runRecord, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult(runRecord.NextNodeIndex());
		}
		return Task.FromResult(TryParseProgress(context, screen, config, out int? nextNodeIndex) ? nextNodeIndex : runRecord.NextNodeIndex());
	}

	/// <inheritdoc />
	public Task<OperationResult> SelectNodeAsync(ZContext context, int nodeIndex, Mat? screen)
	{
		string text = $"节点-{nodeIndex:00}";
		if (screen == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		OperationResult operationResult = ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, "式舆防卫战", text), text);
		if (operationResult.IsSuccess)
		{
			return Task.FromResult(operationResult);
		}
		if (ScreenUtils.FindArea(context, screen, "式舆防卫战", "下一步") == FindAreaResultEnum.True)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "下一步"));
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("式舆防卫战", "节点区域");
		if (area == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "区域未配置 节点区域"));
		}
		OneDragon.Core.Abstractions.Geometry.Point center = area.Center;
		OneDragon.Core.Abstractions.Geometry.Point end = center + new OneDragon.Core.Abstractions.Geometry.Point(-300, 0);
		context.Controller?.DragTo(end, center);
		return Task.FromResult(new OperationResult(IsSuccess: false, operationResult.Status));
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<DefensePhaseTeamInfo>> CalculateTeamsAsync(ZContext context, ShiyuDefenseConfig config, int nodeIndex, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult((IReadOnlyList<DefensePhaseTeamInfo>)Array.Empty<DefensePhaseTeamInfo>());
		}
		IReadOnlyList<DefensePhaseTeamInfo> detectedPhaseList = DetectPhaseTeams(context, screen, "式舆防卫战", 2, 2);
		return Task.FromResult(ShiyuDefenseTeamUtils.CalculateTeams(config, context.TeamConfig.TeamList, detectedPhaseList));
	}

	/// <inheritdoc />
	public Task<OperationResult> EnterTeamSelectionAsync(ZContext context)
	{
		return Task.FromResult(ClickArea(context, "式舆防卫战", "角色头像"));
	}

	/// <inheritdoc />
	public Task<OperationResult> PrepareMultiRoomAsync(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		if (ScreenUtils.FindArea(context, screen, "式舆防卫战-三间选择", "确认") == FindAreaResultEnum.True)
		{
			OperationResult operationResult = ClickArea(context, "式舆防卫战-三间选择", "确认");
			return Task.FromResult(operationResult.IsSuccess ? new OperationResult(IsSuccess: true, "点击确认") : new OperationResult(IsSuccess: false, "点击确认失败"));
		}
		if (ScreenUtils.FindArea(context, screen, "式舆防卫战-三间选择", "本期最佳总分") != FindAreaResultEnum.True)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "等待多间模式画面"));
		}
		foreach (string roomName in ShiyuDefenseConstants.RoomNames)
		{
			OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("式舆防卫战-三间选择", roomName);
			string text = string.Concat(from result in context.OcrService.GetOcrResultList(screen, area?.ColorRange, area?.Rect)
				select result.Text).Trim();
			if (text.Length > 0 && !string.Equals(text, "0", StringComparison.Ordinal))
			{
				ClickArea(context, "式舆防卫战-三间选择", "重置全部");
				return Task.FromResult(new OperationResult(IsSuccess: true, "已重置"));
			}
		}
		return Task.FromResult(new OperationResult(IsSuccess: true, "多间模式"));
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<DefensePhaseTeamInfo>> CalculateMultiRoomTeamsAsync(ZContext context, ShiyuDefenseConfig config, int nodeIndex, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult((IReadOnlyList<DefensePhaseTeamInfo>)Array.Empty<DefensePhaseTeamInfo>());
		}
		List<DefensePhaseTeamInfo> teams = DetectMultiRoomTeams(context, screen);
		List<int> list = (from item in teams.Select((DefensePhaseTeamInfo team, int index) => new { team, index })
			where item.team.TeamIndex != -1
			select item.index).ToList();
		IReadOnlyList<DefensePhaseTeamInfo> readOnlyList = list.Select((int index) => teams[index]).ToArray();
		if (readOnlyList.Count > 0)
		{
			IReadOnlyList<DefensePhaseTeamInfo> readOnlyList2 = ShiyuDefenseTeamUtils.CalculateTeams(config, context.TeamConfig.TeamList, readOnlyList);
			for (int num = 0; num < list.Count && num < readOnlyList2.Count; num++)
			{
				teams[list[num]] = readOnlyList2[num];
			}
		}
		return Task.FromResult((IReadOnlyList<DefensePhaseTeamInfo>)teams);
	}

	/// <inheritdoc />
	public Task<OperationResult> ChooseTeamAsync(ZContext context, IReadOnlyList<int> teamIndexes)
	{
		return new ChoosePredefinedTeam(context, teamIndexes).ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> SelectRoomAsync(ZContext context, int roomIndex, Mat? screen)
	{
		if ((uint)roomIndex >= (uint)ShiyuDefenseConstants.RoomNames.Count)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, $"房间下标无效 {roomIndex}"));
		}
		string text = ShiyuDefenseConstants.RoomNames[roomIndex];
		return Task.FromResult(FindAndClickArea(context, screen, "式舆防卫战-三间选择", "前往" + text));
	}

	/// <inheritdoc />
	public Task<OperationResult> DeployAsync(ZContext context)
	{
		return new Deploy(context).ExecuteAsync();
	}

	/// <inheritdoc />
	public async Task<OperationResult> WaitAndChooseMultiRoomTeamAsync(ZContext context, int teamIndex, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		if (ScreenUtils.FindArea(context, screen, "实战模拟室", "预备编队") == FindAreaResultEnum.True)
		{
			OperationResult choose = await ChooseTeamAsync(context, new int[] { teamIndex }).ConfigureAwait(continueOnCapturedContext: false);
			return choose.IsSuccess ? new OperationResult(IsSuccess: true, "预备编队完成") : choose;
		}
		return ClickArea(context, "实战模拟室", "下一步");
	}

	/// <inheritdoc />
	public Task<OperationResult> BattleAsync(ZContext context, int teamIndex)
	{
		return new ShiyuDefenseBattle(context, teamIndex).ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> ExitMultiRoomAfterBattleAsync(ZContext context, Mat? screen)
	{
		return Task.FromResult(FindAndClickArea(context, screen, "式舆防卫战", "战斗结束-退出"));
	}

	/// <inheritdoc />
	public Task<OperationResult> BackToMainScreenAsync(ZContext context, Mat? screen)
	{
		if (screen != null && ScreenUtils.FindArea(context, screen, "式舆防卫战", "前哨档案") == FindAreaResultEnum.True)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "式舆防卫战"));
		}
		return Task.FromResult(ClickArea(context, "菜单", "返回"));
	}

	/// <inheritdoc />
	public Task<OperationResult> RecoverFromMultiRoomFailureAsync(ZContext context, Mat? screen)
	{
		if (screen != null && ScreenUtils.FindArea(context, screen, "式舆防卫战", "前哨档案") == FindAreaResultEnum.True)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "前哨档案"));
		}
		return Task.FromResult(ClickArea(context, "菜单", "返回"));
	}

	/// <inheritdoc />
	public Task<OperationResult> AdvanceAfterBattleAsync(ZContext context, int currentNodeIndex, ShiyuDefenseConfig config, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		if (ScreenUtils.FindArea(context, screen, "式舆防卫战", "下一步") == FindAreaResultEnum.True)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "下一步"));
		}
		if (currentNodeIndex == config.CriticalMaxNodeIndex)
		{
			return Task.FromResult(FindAndClickArea(context, screen, "式舆防卫战", "战斗结束-退出"));
		}
		OperationResult operationResult = ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, "式舆防卫战", "战斗结束-下一防线"), "战斗结束-下一防线");
		if (operationResult.IsSuccess)
		{
			return Task.FromResult(operationResult);
		}
		FindAndClickArea(context, screen, "式舆防卫战", "战斗结束-退出");
		OperationResult nodeFiveResult = FindAndClickArea(context, screen, "式舆防卫战", "节点-05");
		if (nodeFiveResult.IsSuccess)
		{
			// 前四关已打完、进入第五关，节点下标需要显式跳到 5，通过 Data 交给调用方回写状态
			return Task.FromResult(new OperationResult(IsSuccess: true, "节点-05", 5));
		}
		return Task.FromResult(nodeFiveResult);
	}

	/// <inheritdoc />
	public Task<OperationResult> FinishAllNodesAsync(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		if (ScreenUtils.FindArea(context, screen, "式舆防卫战", "前哨档案") == FindAreaResultEnum.True)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "前哨档案"));
		}
		return Task.FromResult(FindAndClickArea(context, screen, "式舆防卫战", "战斗结束-退出"));
	}

	/// <inheritdoc />
	public Task<OperationResult> ClaimRewardAsync(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		if (ScreenUtils.FindArea(context, screen, "式舆防卫战", "领取奖励-界面") == FindAreaResultEnum.True)
		{
			return Task.FromResult(ClickArea(context, "式舆防卫战", "全部领取"));
		}
		return Task.FromResult(ClickArea(context, "式舆防卫战", "奖励入口"));
	}

	/// <inheritdoc />
	public Task<OperationResult> CloseRewardAsync(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		if (ScreenUtils.FindArea(context, screen, "式舆防卫战", "前哨档案") == FindAreaResultEnum.True)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "前哨档案"));
		}
		OperationResult operationResult = ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, "式舆防卫战", "领取奖励-确认"), "领取奖励-确认");
		if (operationResult.IsSuccess)
		{
			return Task.FromResult(operationResult);
		}
		OperationResult result = ClickArea(context, "式舆防卫战", "领取奖励-关闭");
		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<OperationResult> BackToWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}

	private static bool TryParseProgress(ZContext context, Mat screen, ShiyuDefenseConfig config, out int? nextNodeIndex)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("式舆防卫战", "剧变节点进度");
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, area?.ColorRange, area?.Rect);
		if (ocrResultList.Count == 0)
		{
			nextNodeIndex = null;
			return false;
		}
		string input = ocrResultList.FirstOrDefault((OcrMatchResult result) => Regex.IsMatch(result.Text, "\\d"))?.Text ?? ocrResultList[0].Text;
		MatchCollection digits = Regex.Matches(input, "\\d");
		if (digits.Count >= 2)
		{
			int current = int.Parse(digits[0].Value);
			int total = config.CriticalMaxNodeIndex = int.Parse(digits[^1].Value);
			context.Logger.Information("剧变节点进度 {Current}/{Total}", current, total);
			int next = current + 1;
			nextNodeIndex = (next > total) ? null : new int?(next);
			return true;
		}
		if (digits.Count == 1)
		{
			int total = config.CriticalMaxNodeIndex = int.Parse(digits[0].Value);
			context.Logger.Information("剧变节点进度 已完成 {Total}", total);
			nextNodeIndex = null;
			return true;
		}
		context.Logger.Information("OCR 进度解析失败: {ProgressText}", input);
		nextNodeIndex = null;
		return false;
	}

	private static IReadOnlyList<DefensePhaseTeamInfo> DetectPhaseTeams(ZContext context, Mat screen, string screenName, int phaseCount, int typeCount)
	{
		List<DefensePhaseTeamInfo> list = new List<DefensePhaseTeamInfo>();
		for (int i = 0; i < phaseCount; i++)
		{
			List<DmgTypeEnum> list2 = new List<DmgTypeEnum>();
			List<DmgTypeEnum> list3 = new List<DmgTypeEnum>();
			for (int j = 0; j < typeCount; j++)
			{
				list2.Add(CheckTypeByArea(context, screen, screenName, $"弱点-{i + 1}-{j + 1}"));
				list3.Add(CheckTypeByArea(context, screen, screenName, $"抗性-{i + 1}-{j + 1}"));
			}
			list.Add(new DefensePhaseTeamInfo(list2, list3));
		}
		return list;
	}

	private static List<DefensePhaseTeamInfo> DetectMultiRoomTeams(ZContext context, Mat screen)
	{
		List<DefensePhaseTeamInfo> list = new List<DefensePhaseTeamInfo>();
		foreach (string roomName in ShiyuDefenseConstants.RoomNames)
		{
			OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("式舆防卫战-三间选择", roomName);
			string text = string.Concat(from result in context.OcrService.GetOcrResultList(screen, area?.ColorRange, area?.Rect)
				select result.Text).Trim();
			if (text.Length > 0 && !string.Equals(text, "0", StringComparison.Ordinal))
			{
				list.Add(new DefensePhaseTeamInfo(new DmgTypeEnum[2]
				{
					DmgTypeEnum.UNKNOWN,
					DmgTypeEnum.UNKNOWN
				}, new DmgTypeEnum[2]
				{
					DmgTypeEnum.UNKNOWN,
					DmgTypeEnum.UNKNOWN
				})
				{
					TeamIndex = -1
				});
				continue;
			}
			OneDragon.Core.Screen.ScreenArea area2 = context.ScreenContext.GetArea("式舆防卫战-三间选择", roomName + "属性");
			IReadOnlyList<OcrMatchResult> readOnlyList;
			if (area2 != null)
			{
				readOnlyList = context.OcrService.GetOcrResultList(screen, area2.ColorRange, area2.Rect);
			}
			else
			{
				IReadOnlyList<OcrMatchResult> readOnlyList2 = Array.Empty<OcrMatchResult>();
				readOnlyList = readOnlyList2;
			}
			IReadOnlyList<OcrMatchResult> source = readOnlyList;
			int? num = source.FirstOrDefault((OcrMatchResult result) => result.Text.Contains("强敌抗性", StringComparison.Ordinal))?.Y;
			List<string> list2 = new List<string>();
			List<string> list3 = new List<string>();
			foreach (OcrMatchResult item in source.Where((OcrMatchResult result) => result.Text.Contains("属性", StringComparison.Ordinal)))
			{
				if (num.HasValue && item.Y >= num)
				{
					list3.Add(item.Text);
				}
				else
				{
					list2.Add(item.Text);
				}
			}
			List<DmgTypeEnum> list4 = ExtractDamageTypes(list2);
			List<DmgTypeEnum> list5 = ExtractDamageTypes(list3);
			while (list4.Count < 2)
			{
				list4.Add(DmgTypeEnum.UNKNOWN);
			}
			while (list5.Count < 2)
			{
				list5.Add(DmgTypeEnum.UNKNOWN);
			}
			list.Add(new DefensePhaseTeamInfo(list4.Take(2).ToArray(), list5.Take(2).ToArray())
			{
				TeamIndex = 0
			});
		}
		return list;
	}

	private static DmgTypeEnum CheckTypeByArea(ZContext context, Mat screen, string screenName, string areaName)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea(screenName, areaName);
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, area?.ColorRange, area?.Rect);
		foreach (OcrMatchResult item in ocrResultList)
		{
			DmgTypeEnum dmgTypeEnum = ParseDamageType(item.Text);
			if (dmgTypeEnum != DmgTypeEnum.UNKNOWN)
			{
				return dmgTypeEnum;
			}
		}
		return DmgTypeEnum.UNKNOWN;
	}

	private static List<DmgTypeEnum> ExtractDamageTypes(IEnumerable<string> texts)
	{
		string text = string.Join(' ', texts);
		List<DmgTypeEnum> list = new List<DmgTypeEnum>();
		foreach (DmgTypeEnum item in from type in Enum.GetValues<DmgTypeEnum>()
			where type != DmgTypeEnum.UNKNOWN
			select type)
		{
			if (text.Contains(item.GetStringValue(), StringComparison.Ordinal) || text.Contains(item.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				list.Add(item);
			}
		}
		return list;
	}

	private static DmgTypeEnum ParseDamageType(string text)
	{
		DmgTypeEnum[] array = (from type in Enum.GetValues<DmgTypeEnum>()
			where type != DmgTypeEnum.UNKNOWN
			select type).ToArray();
		string[] targetWords = array.Select((DmgTypeEnum type) => type.GetStringValue()).ToArray();
		int? num = StringUtils.FindBestMatchByDifflib(text, targetWords);
		return (!num.HasValue) ? DmgTypeEnum.UNKNOWN : array[num.Value];
	}

	private static OperationResult FindAndClickArea(ZContext context, Mat? screen, string screenName, string areaName)
	{
		return (screen == null) ? new OperationResult(IsSuccess: false, "未获取截图") : ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, screenName, areaName), areaName);
	}

	private static OperationResult ClickArea(ZContext context, string screenName, string areaName)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea(screenName, areaName);
		if (area == null)
		{
			return new OperationResult(IsSuccess: false, "区域未配置 " + areaName);
		}
		ControllerBase? controller = context.Controller;
		OperationResult result;
		if (controller != null)
		{
			OneDragon.Core.Abstractions.Geometry.Point? position = area.Center;
			bool pcAlt = area.PcAlt;
			string gamepadKey = area.GamepadKey;
			if (controller.Click(position, null, pcAlt, gamepadKey))
			{
				result = new OperationResult(IsSuccess: true, areaName);
				goto IL_0088;
			}
		}
		result = new OperationResult(IsSuccess: false, "点击失败 " + areaName);
		goto IL_0088;
		IL_0088:
		return result;
	}

	private static OperationResult ConvertClickResult(OcrClickResultEnum result, string targetName)
	{
		if (1 == 0)
		{
		}
		OperationResult result2 = result switch
		{
			OcrClickResultEnum.OcrClickSuccess => new OperationResult(IsSuccess: true, targetName), 
			OcrClickResultEnum.AreaNoConfig => new OperationResult(IsSuccess: false, "区域未配置 " + targetName), 
			OcrClickResultEnum.OcrClickFail => new OperationResult(IsSuccess: false, "点击失败 " + targetName), 
			_ => new OperationResult(IsSuccess: false, "未找到 " + targetName), 
		};
		if (1 == 0)
		{
		}
		return result2;
	}
}
