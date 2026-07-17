using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Vision;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 默认式舆防卫战战斗服务。
/// </summary>
public sealed class DefaultShiyuDefenseBattleServices : IShiyuDefenseBattleServices
{
	private readonly ImageAnalysisPipelineRunner _pipelineRunner;

	private int _moveTimes;

	private int _interactFoundTimes;

	private DateTimeOffset? _noCountdownStartTime;

	private DateTimeOffset _lastCountdownCheckTime = DateTimeOffset.MinValue;

	/// <summary>
	/// 初始化默认式舆防卫战战斗服务。
	/// </summary>
	public DefaultShiyuDefenseBattleServices(ImageAnalysisPipelineRunner? pipelineRunner = null)
	{
		_pipelineRunner = pipelineRunner ?? new ImageAnalysisPipelineRunner();
	}

	/// <inheritdoc />
	public void LoadAutoOperation(ZContext context, int predefinedTeamIndex)
	{
		string opName = context.TeamConfig.TeamList.ElementAtOrDefault(predefinedTeamIndex)?.AutoBattle ?? context.BattleAssistantConfig.AutoBattleConfig;
		context.AutoBattleContext.LastCheckEndResult = null;
		context.AutoBattleContext.InitAutoOp(opName);
	}

	/// <inheritdoc />
	public bool IsBattleScreenReady(ZContext context, Mat? screen)
	{
		return screen != null && ScreenUtils.FindArea(context, screen, "战斗画面", "按键-普通攻击") == FindAreaResultEnum.True;
	}

	/// <inheritdoc />
	public OperationResult PrepareBattle(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "等待战斗准备");
		}
		if (HasShiyuCountdown(context, screen))
		{
			context.AutoBattleContext.StartAutoBattle();
			_moveTimes = 0;
			return new OperationResult(IsSuccess: true);
		}
		float? num = context.AutoBattleContext.CheckBattleDistance(screen);
		if (!TryGetBattleDistancePosition(context, screen, out var position))
		{
			if (context.AutoBattleContext.WithoutDistanceTimes >= 10)
			{
				context.AutoBattleContext.StartAutoBattle();
				_moveTimes = 0;
				return new OperationResult(IsSuccess: true);
			}
			return new OperationResult(IsSuccess: false, "等待战斗准备");
		}
		if (_moveTimes >= 20)
		{
			return new OperationResult(IsSuccess: false, "移动失败");
		}
		if (!(context.Controller is IZzzControllerActions zzzControllerActions) || !num.HasValue)
		{
			return new OperationResult(IsSuccess: false, "移动失败");
		}
		if (position.Value.X < 900)
		{
			zzzControllerActions.TurnByDistance(-50f);
			return new OperationResult(IsSuccess: false, "等待战斗后移动");
		}
		if (position.Value.X > 1100)
		{
			zzzControllerActions.TurnByDistance(50f);
			return new OperationResult(IsSuccess: false, "等待战斗后移动");
		}
		TimeSpan value = TimeSpan.FromSeconds(Math.Min((double)num.Value / 7.2, 4.0));
		zzzControllerActions.MoveW(press: true, value, release: true);
		_moveTimes++;
		return new OperationResult(IsSuccess: false, "等待战斗后移动");
	}

	/// <inheritdoc />
	public OperationResult RunAutoBattle(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc)
	{
		string lastCheckEndResult = context.AutoBattleContext.LastCheckEndResult;
		if (!string.IsNullOrWhiteSpace(lastCheckEndResult))
		{
			context.AutoBattleContext.StopAutoBattle();
			return new OperationResult(IsSuccess: true, lastCheckEndResult);
		}
		if (screen == null || screen.Empty())
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		DateTimeOffset dateTimeOffset = screenshotTimeUtc ?? DateTimeOffset.UtcNow;
		if (context.AutoBattleContext.CheckBattleState(screen, dateTimeOffset, checkBattleEndNormalResult: true, checkBattleEndHollowResult: false, checkBattleEndDefenseResult: true))
		{
			if (dateTimeOffset - _lastCountdownCheckTime >= TimeSpan.FromSeconds(1L))
			{
				_lastCountdownCheckTime = dateTimeOffset;
				if (HasShiyuCountdown(context, screen))
				{
					_noCountdownStartTime = null;
				}
				else
				{
					DateTimeOffset valueOrDefault = _noCountdownStartTime.GetValueOrDefault();
					if (!_noCountdownStartTime.HasValue)
					{
						valueOrDefault = dateTimeOffset;
						_noCountdownStartTime = valueOrDefault;
					}
					valueOrDefault = dateTimeOffset;
					DateTimeOffset? noCountdownStartTime = _noCountdownStartTime;
					if (valueOrDefault - noCountdownStartTime >= TimeSpan.FromSeconds(5L))
					{
						_noCountdownStartTime = null;
						context.AutoBattleContext.StopAutoBattle();
						return new OperationResult(IsSuccess: true, "需要移动");
					}
				}
			}
		}
		else
		{
			_noCountdownStartTime = null;
			_interactFoundTimes = (FindArea(context, screen, "战斗画面", "按键-交互") ? (_interactFoundTimes + 1) : 0);
			if (_interactFoundTimes >= 10)
			{
				context.AutoBattleContext.StopAutoBattle();
				return new OperationResult(IsSuccess: true, "需要移动");
			}
		}
		return new OperationResult(IsSuccess: false, "自动战斗中");
	}

	/// <inheritdoc />
	public OperationResult MoveAfterBattle(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		if (HasShiyuCountdown(context, screen))
		{
			context.AutoBattleContext.StartAutoBattle();
			_moveTimes = 0;
			_noCountdownStartTime = null;
			return new OperationResult(IsSuccess: true, "返回战斗");
		}
		if (FindArea(context, screen, "战斗画面", "按键-交互"))
		{
			if (context.Controller is IZzzControllerActions zzzControllerActions)
			{
				zzzControllerActions.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
				return new OperationResult(IsSuccess: false, "等待交互完成");
			}
			return new OperationResult(IsSuccess: false, "移动失败");
		}
		if (!FindArea(context, screen, "战斗画面", "按键-普通攻击"))
		{
			return new OperationResult(IsSuccess: true, "下一阶段");
		}
		if (!(context.Controller is IZzzControllerActions zzzControllerActions2))
		{
			return new OperationResult(IsSuccess: false, "移动失败");
		}
		AutoBattleUtils.SwitchToBestAgentForMoving(context);
		float? num = context.AutoBattleContext.CheckBattleDistance(screen);
		OneDragon.Core.Abstractions.Geometry.Point? point = null;
		float? num2 = null;
		if (TryGetBattleDistancePosition(context, screen, out var position) && num.HasValue)
		{
			float valueOrDefault = num.GetValueOrDefault();
			if (true)
			{
				point = position;
				num2 = valueOrDefault;
				goto IL_01c6;
			}
		}
		OneDragon.Core.Abstractions.Geometry.Point? point2 = (point = GetTeleportPoint(context, screen));
		if (point2.HasValue)
		{
			num2 = 5f;
		}
		goto IL_01c6;
		IL_028d:
		if (_moveTimes >= 60)
		{
			return new OperationResult(IsSuccess: false, "移动失败");
		}
		return new OperationResult(IsSuccess: false, "等待战斗后移动");
		IL_01c6:
		if (point.HasValue)
		{
			OneDragon.Core.Abstractions.Geometry.Point valueOrDefault2 = point.GetValueOrDefault();
			if (num2.HasValue)
			{
				float valueOrDefault3 = num2.GetValueOrDefault();
				if (true)
				{
					int num3 = context.ProjectConfig.ScreenStandardWidth / 2;
					int num4 = valueOrDefault2.X - num3;
					if (Math.Abs(num4) > 50)
					{
						zzzControllerActions2.TurnByDistance((num4 > 0) ? 50 : (-50));
					}
					else
					{
						zzzControllerActions2.MoveW(press: true, TimeSpan.FromSeconds(Math.Min((double)valueOrDefault3 / 7.2, 1.0)), release: true);
						_moveTimes++;
					}
					goto IL_028d;
				}
			}
		}
		zzzControllerActions2.TurnByDistance(200f);
		goto IL_028d;
	}

	/// <inheritdoc />
	public void StopAutoBattle(ZContext context)
	{
		context.AutoBattleContext.StopAutoBattle();
	}

	/// <inheritdoc />
	public OperationResult PrepareVoluntaryExit(ZContext context, Mat? screen)
	{
		if (screen != null && FindArea(context, screen, "式舆防卫战", "退出战斗"))
		{
			return new OperationResult(IsSuccess: true, "退出战斗");
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("战斗画面", "菜单");
		if (area == null)
		{
			return new OperationResult(IsSuccess: false, "区域未配置 菜单");
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
				result = new OperationResult(IsSuccess: true, "菜单");
				goto IL_00b8;
			}
		}
		result = new OperationResult(IsSuccess: false, "点击失败 菜单");
		goto IL_00b8;
		IL_00b8:
		return result;
	}

	private static bool FindArea(ZContext context, Mat screen, string screenName, string areaName)
	{
		return ScreenUtils.FindArea(context, screen, screenName, areaName) == FindAreaResultEnum.True;
	}

	private bool HasShiyuCountdown(ZContext context, Mat screen)
	{
		ImageAnalysisPipelineRunResult imageAnalysisPipelineRunResult = _pipelineRunner.Run(context, "防卫战倒计时", screen);
		ImageAnalysisPipelineRunResult imageAnalysisPipelineRunResult2 = _pipelineRunner.Run(context, "防卫战倒计时-精英", screen);
		bool flag = imageAnalysisPipelineRunResult.IsSuccess && imageAnalysisPipelineRunResult.Contours.Count == 4;
		bool flag2 = imageAnalysisPipelineRunResult2.IsSuccess && imageAnalysisPipelineRunResult2.Contours.Count == 4;
		return flag || flag2;
	}

	private OneDragon.Core.Abstractions.Geometry.Point? GetTeleportPoint(ZContext context, Mat screen)
	{
		ImageAnalysisPipelineRunResult imageAnalysisPipelineRunResult = _pipelineRunner.Run(context, "防卫战空洞传送点", screen);
		if (!imageAnalysisPipelineRunResult.IsSuccess)
		{
			return null;
		}
		ImageAnalysisContour imageAnalysisContour = imageAnalysisPipelineRunResult.Contours.OrderByDescending((ImageAnalysisContour contour) => contour.Points.Length).FirstOrDefault();
		return ((object)imageAnalysisContour == null) ? ((OneDragon.Core.Abstractions.Geometry.Point?)null) : new OneDragon.Core.Abstractions.Geometry.Point?(new OneDragon.Core.Abstractions.Geometry.Point(imageAnalysisContour.Rect.X + imageAnalysisContour.Rect.Width / 2, imageAnalysisContour.Rect.Y + imageAnalysisContour.Rect.Height / 2));
	}

	private static bool TryGetBattleDistancePosition(ZContext context, Mat screen, out OneDragon.Core.Abstractions.Geometry.Point? position)
	{
		position = null;
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("战斗画面", "距离显示区域");
		if (area == null)
		{
			return false;
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, null, area.Rect);
		OcrMatchResult ocrMatchResult = null;
		float? num = null;
		int num2 = context.ProjectConfig.ScreenStandardWidth / 2;
		foreach (OcrMatchResult item in ocrResultList)
		{
			Match match = Regex.Match(item.Text, "\\d+(\\.\\d+)?(?=m)");
			if (match.Success && float.TryParse(match.Value, CultureInfo.InvariantCulture, out var result) && (ocrMatchResult == null || Math.Abs(item.Center.X - num2) < Math.Abs(ocrMatchResult.Center.X - num2)))
			{
				ocrMatchResult = item;
				num = result;
			}
		}
		if (ocrMatchResult == null || !num.HasValue)
		{
			return false;
		}
		position = ocrMatchResult.Center;
		return true;
	}
}
