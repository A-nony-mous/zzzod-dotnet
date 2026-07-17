using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.Hdd;

namespace ZzzOd.GameLogic.Application.LifeOnLine;

/// <summary>
/// 默认生命热线流程服务。
/// </summary>
public sealed class DefaultLifeOnLineOperationServices : ILifeOnLineOperationServices
{
	private const string HddMissionName = "作战真拿命验收";

	private const string KeySimOperationName = "真拿命验收";

	private const string MissionScreenName = "真拿命验收";

	/// <inheritdoc />
	public Task<OperationResult> TransportToHddAsync(ZContext context)
	{
		return new Transport(context, "录像店", "HDD").ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> WaitNormalWorldAsync(ZContext context)
	{
		return new WaitNormalWorld(context).ExecuteAsync();
	}

	/// <inheritdoc />
	public bool IsHddStreetVisible(ZContext context, Mat? screen)
	{
		return FindArea(context, screen, "HDD", "街区");
	}

	/// <inheritdoc />
	public void Interact(ZContext context)
	{
		if (context.Controller is IZzzControllerActions zzzControllerActions)
		{
			zzzControllerActions.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
		}
	}

	/// <inheritdoc />
	public Task<OperationResult> EnterMissionAsync(ZContext context, int predefinedTeamIndex)
	{
		return new EnterHddMission(context, "第二章间章", "战斗委托", "作战真拿命验收", predefinedTeamIndex).ExecuteAsync();
	}

	/// <inheritdoc />
	public bool IsBattleScreenReady(ZContext context, Mat? screen)
	{
		return FindArea(context, screen, "战斗画面", "按键-普通攻击");
	}

	/// <inheritdoc />
	public Task<OperationResult> RunKeySimAsync(ZContext context)
	{
		return new KeySimRunner(context, "真拿命验收").ExecuteAsync();
	}

	/// <inheritdoc />
	public bool IsDialogPersonVisible(ZContext context, Mat? screen)
	{
		return FindArea(context, screen, "真拿命验收", "对话人");
	}

	/// <inheritdoc />
	public bool IsBattleResultCompleteVisible(ZContext context, Mat? screen)
	{
		return FindArea(context, screen, "战斗画面", "战斗结果-完成");
	}

	/// <inheritdoc />
	public string? ClickFirstDialogOption(ZContext context, Mat? screen)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("真拿命验收", "对话选项");
		if (screen == null || area == null)
		{
			return null;
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect);
		OcrMatchResult ocrMatchResult = ocrResultList.FirstOrDefault();
		if (ocrMatchResult == null)
		{
			return null;
		}
		ControllerBase? controller = context.Controller;
		object result;
		if (controller != null)
		{
			OneDragon.Core.Abstractions.Geometry.Point? position = ocrMatchResult.Center;
			bool pcAlt = area.PcAlt;
			string gamepadKey = area.GamepadKey;
			if (controller.Click(position, null, pcAlt, gamepadKey))
			{
				result = ocrMatchResult.Text;
				goto IL_00bc;
			}
		}
		result = null;
		goto IL_00bc;
		IL_00bc:
		return (string?)result;
	}

	/// <inheritdoc />
	public OperationResult ClickMenuBack(ZContext context)
	{
		return ClickArea(context, "菜单", "返回");
	}

	/// <inheritdoc />
	public OperationResult ClickBattleResultComplete(ZContext context, Mat? screen)
	{
		return FindAndClickArea(context, screen, "战斗画面", "战斗结果-完成");
	}

	/// <inheritdoc />
	public Task<OperationResult> WaitNormalWorldOnceAsync(ZContext context)
	{
		return new WaitNormalWorld(context, checkOnce: true).ExecuteAsync();
	}

	/// <inheritdoc />
	public OperationResult ClickHddBlank(ZContext context)
	{
		return ClickArea(context, "HDD", "空白");
	}

	/// <inheritdoc />
	public Task<OperationResult> BackToWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}

	/// <inheritdoc />
	public bool IsExitBattleVisible(ZContext context, Mat? screen)
	{
		return FindArea(context, screen, "恶名狩猎", "退出战斗");
	}

	/// <inheritdoc />
	public OperationResult ClickBattleMenu(ZContext context)
	{
		return ClickArea(context, "战斗画面", "菜单");
	}

	/// <inheritdoc />
	public OperationResult ClickExitBattle(ZContext context, Mat? screen)
	{
		return FindAndClickArea(context, screen, "恶名狩猎", "退出战斗");
	}

	/// <inheritdoc />
	public OperationResult ClickExitBattleConfirm(ZContext context, Mat? screen)
	{
		return FindAndClickArea(context, screen, "恶名狩猎", "退出战斗-确认");
	}

	private static bool FindArea(ZContext context, Mat? screen, string screenName, string areaName)
	{
		return screen != null && ScreenUtils.FindArea(context, screen, screenName, areaName) == FindAreaResultEnum.True;
	}

	private static OperationResult FindAndClickArea(ZContext context, Mat? screen, string screenName, string areaName)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		Thread.Sleep(TimeSpan.FromMilliseconds(300L));
		return ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, screenName, areaName), areaName);
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
